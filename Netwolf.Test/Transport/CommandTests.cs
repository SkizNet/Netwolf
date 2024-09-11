using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Netwolf.Transport.Extensions.DependencyInjection;
using Netwolf.Transport.IRC;
using Netwolf.PluginFramework.Commands;

namespace Netwolf.Test.Transport;

/// <summary>
/// Tests for command parsing
/// Adapted from https://github.com/ircdocs/parser-tests, which is licensed under CC0
/// </summary>
[TestClass]
public class CommandTests
{
    private const string? NoValue = null;

    private ServiceProvider Container { get; init; }

    private ICommandFactory CommandFactory { get; init; }

    static IEnumerable<object?[]> TestCommandParseData => [
        ["foo bar baz asdf", null, "FOO", new string[] { "bar", "baz", "asdf" }, null],
        [":coolguy foo bar baz asdf", "coolguy", "FOO", new string[] { "bar", "baz", "asdf" }, null],
        ["foo bar baz :asdf quux", null, "FOO", new string[] { "bar", "baz", "asdf quux" }, null],
        ["foo bar baz :", null, "FOO", new string[] { "bar", "baz", "" }, null],
        ["foo bar baz ::asdf", null, "FOO", new string[] { "bar", "baz", ":asdf" }, null],
        [":coolguy foo bar baz :asdf quux", "coolguy", "FOO", new string[] { "bar", "baz", "asdf quux" }, null],
        [":coolguy foo bar baz :  asdf quux ", "coolguy", "FOO", new string[] { "bar", "baz", "  asdf quux " }, null],
        [":coolguy PRIVMSG bar :lol :) ", "coolguy", "PRIVMSG", new string[] { "bar", "lol :) " }, null],
        [":coolguy foo bar baz :", "coolguy", "FOO", new string[] { "bar", "baz", "" }, null],
        [":coolguy foo bar baz :  ", "coolguy", "FOO", new string[] { "bar", "baz", "  " }, null],
        ["@a=b;c=32;k;rt=ql7 foo", null, "FOO", Array.Empty<string>(), new { a = "b", c = "32", k = NoValue, rt = "ql7" }],
        ["@a=b\\\\and\\nk;c=72\\s45;d=gh\\:764 foo", null, "FOO", Array.Empty<string>(), new { a = "b\\and\nk", c = "72 45", d = "gh;764" }],
        // spec allows us to normalize empty tag values into missing tag values, but forbids us from treating missing and empty differently
        ["@c;h=;a=b :quux ab cd", "quux", "AB", new string[] { "cd" }, new { c = NoValue, h = NoValue, a = "b" }],
        // different forms of last param
        [":src JOIN #chan", "src", "JOIN", new string[] { "#chan" }, null],
        [":src JOIN :#chan", "src", "JOIN", new string[] { "#chan" }, null],
        // with and without last param
        [":src AWAY", "src", "AWAY", Array.Empty<string>(), null],
        [":src AWAY ", "src", "AWAY", Array.Empty<string>(), null],
        [":cool\tguy foo bar baz", "cool\tguy", "FOO", new string[] { "bar", "baz" }, null],
        // with weird control codes in the source
        // C# parses up to 4 hex characters after \x, whereas the source doc assumes only 2 characters are parsed.
        // An extra 00 was prefixed for each \x to accomodate this
        [
            ":coolguy!ag@net\x0035w\x0003ork.admin PRIVMSG foo :bar baz",
            "coolguy!ag@net\x0035w\x0003ork.admin",
            "PRIVMSG",
            new string[] { "foo", "bar baz" },
            null
        ],
        [
            ":coolguy!~ag@n\x0002et\x000305w\x000fork.admin PRIVMSG foo :bar baz",
            "coolguy!~ag@n\x0002et\x000305w\x000fork.admin",
            "PRIVMSG",
            new string[] { "foo", "bar baz" },
            null
        ],
        [
            "@tag1=value1;tag2;vendor1/tag3=value2;vendor2/tag4= :irc.example.com COMMAND param1 param2 :param3 param3",
            "irc.example.com",
            "COMMAND",
            new string[] { "param1", "param2", "param3 param3" },
            new Dictionary<string, string?> { { "tag1", "value1" }, { "tag2", null }, { "vendor1/tag3", "value2" }, { "vendor2/tag4", null } }
        ],
        [":irc.example.com COMMAND param1 param2 :param3 param3", "irc.example.com", "COMMAND", new string[] { "param1", "param2", "param3 param3" }, null],
        [
            "@tag1=value1;tag2;vendor1/tag3=value2;vendor2/tag4 COMMAND param1 param2 :param3 param3",
            null,
            "COMMAND",
            new string[] { "param1", "param2", "param3 param3" },
            new Dictionary<string, string?> { { "tag1", "value1" }, { "tag2", null }, { "vendor1/tag3", "value2" }, { "vendor2/tag4", null } }
        ],
        ["@foo=\\\\\\\\\\:\\\\s\\s\\r\\n COMMAND", null, "COMMAND", Array.Empty<string>(), new { foo = "\\\\;\\s \r\n" }],
        // broken messages from unreal
        [
            ":gravel.mozilla.org 432  #momo :Erroneous Nickname: Illegal characters",
            "gravel.mozilla.org",
            "432",
            new string[] { "#momo", "Erroneous Nickname: Illegal characters" },
            null
        ],
        [":gravel.mozilla.org MODE #tckk +n ", "gravel.mozilla.org", "MODE", new string[] { "#tckk", "+n" }, null],
        [":services.esper.net MODE #foo-bar +o foobar  ", "services.esper.net", "MODE", new string[] { "#foo-bar", "+o", "foobar" }, null],
        // tag values should be parsed char-at-a-time to prevent wayward replacements.
        ["@tag1=value\\\\ntest COMMAND", null, "COMMAND", Array.Empty<string>(), new { tag1 = "value\\ntest" }],
        // If a tag value has a slash followed by a character which doesn't need to be escaped, the slash should be dropped.
        ["@tag1=value\\1 COMMAND", null, "COMMAND", Array.Empty<string>(), new { tag1 = "value1" }],
        // A slash at the end of a tag value should be dropped
        ["@tag1=value1\\ COMMAND", null, "COMMAND", Array.Empty<string>(), new { tag1 = "value1" }],
        // Duplicate tags: Parsers SHOULD disregard all but the final occurence
        ["@tag1=1;tag2=3;tag3=4;tag1=5 COMMAND", null, "COMMAND", Array.Empty<string>(), new { tag1 = "5", tag2 = "3", tag3 = "4" }],
        // vendored tags can have the same name as a non-vendored tag
        [
            "@tag1=1;tag2=3;tag3=4;tag1=5;vendor/tag2=8 COMMAND",
            null,
            "COMMAND",
            Array.Empty<string>(),
            new Dictionary<string, string?> { { "tag1", "5" }, { "tag2", "3" }, { "tag3", "4" }, { "vendor/tag2", "8" } }
        ],
        // Some parsers handle /MODE in a special way, make sure they do it right
        [":SomeOp MODE #channel :+i", "SomeOp", "MODE", new string[] { "#channel", "+i" }, null],
        [":SomeOp MODE #channel +oo SomeUser :AnotherUser", "SomeOp", "MODE", new string[] { "#channel", "+oo", "SomeUser", "AnotherUser" }, null],
    ];

    public CommandTests()
    {
        Container = new ServiceCollection()
            .AddLogging(config => config.AddConsole())
            // bring in default Netwolf DI services
            .AddTransportServices()
            .BuildServiceProvider();

        CommandFactory = Container.GetRequiredService<ICommandFactory>();
    }

    [DataTestMethod]
    [DynamicData(nameof(TestCommandParseData))]
    public void Valid_IRC_lines_parse_successfully(string rawCommand, string? source, string verb, string[] args, object? tagsObject)
    {
        var command = CommandFactory.Parse(CommandType.Server, rawCommand);
        Dictionary<string, string?> tags;

        Assert.AreEqual(source, command.Source, "Source differs");
        Assert.AreEqual(verb, command.Verb, "Verb differs");
        CollectionAssert.AreEqual(args, command.Args.ToArray(), "Args differ");
        tags = (tagsObject as Dictionary<string, string?>) ?? tagsObject?.GetType().GetProperties().ToDictionary(x => x.Name, x => x.GetValue(tagsObject)?.ToString()) ?? new();
        CollectionAssert.AreEquivalent(tags, command.Tags.ToDictionary(x => x.Key, x => x.Value), "Tags differ");
    }
}
