using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Transport.Exceptions;
using Netwolf.Transport.Extensions.DependencyInjection;
using Netwolf.Transport.IRC;

namespace Netwolf.Test.Transport;

[TestClass]
public class StateTests
{
    private ServiceProvider Container { get; init; }

    private static NetworkOptions Options => new()
    {
        PrimaryNick = "test",
        Ident = "id",
        RealName = "real name",
        Servers = [new("irc.netwolf.org", 6697)]
    };

    public StateTests()
    {
        Container = new ServiceCollection()
            .AddLogging(config => config.SetMinimumLevel(LogLevel.Trace).AddConsole())
            // bring in default Netwolf DI services
            .AddTransportServices()
            .Replace(ServiceDescriptor.Singleton<IConnectionFactory, FakeConnectionFactory>())
            .BuildServiceProvider();
    }

    [TestMethod]
    public async Task Successfully_self_join_channel()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #TestiNg");
        var info = network.AsNetworkInfo();
        var channel = info.GetChannel("#testing");
        Assert.IsNotNull(channel);
        Assert.AreEqual("#TestiNg", channel.Name);
        Assert.IsTrue(info.Channels.ContainsKey(channel));
        Assert.AreEqual(info.ClientId, channel.Users.Single().Key);
        Assert.AreEqual(string.Empty, channel.Users.Single().Value);
    }

    [TestMethod]
    public async Task Successfully_other_join_channel_regular()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #TestiNg");
        await network.ReceiveLineForUnitTests(":foo!~bar@baz/baz JOIN #TestiNg");
        var info = network.AsNetworkInfo();
        var channel = info.GetChannel("#testing");
        var user = info.GetUserByNick("FOO");
        Assert.IsNotNull(channel);
        Assert.IsNotNull(user);
        Assert.HasCount(2, channel.Users);
        Assert.AreEqual("foo", user.Nick);
        Assert.AreEqual("~bar", user.Ident);
        Assert.AreEqual("baz/baz", user.Host);
        Assert.IsNull(user.Account);
        Assert.AreEqual(string.Empty, user.RealName);
        Assert.IsTrue(channel.Users.ContainsKey(user.Id));
        Assert.IsTrue(user.Channels.ContainsKey(channel.Id));
        Assert.AreEqual(string.Empty, channel.Users[user.Id]);
        Assert.AreEqual(string.Empty, user.Channels[channel.Id]);
    }

    [TestMethod]
    public async Task Successfully_other_join_channel_extended()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":irc.netwolf.org CAP test ACK :extended-join");
        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #TestiNg different :also different");
        await network.ReceiveLineForUnitTests(":foo!~bar@baz/baz JOIN #TestiNg * :UwU");
        var info = network.AsNetworkInfo();
        var channel = info.GetChannel("#testing");
        var user = info.GetUserByNick("FOO");
        Assert.IsNotNull(channel);
        Assert.IsNotNull(user);
        Assert.HasCount(2, channel.Users);
        Assert.AreEqual("foo", user.Nick);
        Assert.AreEqual("~bar", user.Ident);
        Assert.AreEqual("baz/baz", user.Host);
        Assert.IsNull(user.Account);
        Assert.AreEqual("UwU", user.RealName);
        Assert.IsTrue(channel.Users.ContainsKey(user.Id));
        Assert.IsTrue(user.Channels.ContainsKey(channel.Id));
        Assert.AreEqual(string.Empty, channel.Users[user.Id]);
        Assert.AreEqual(string.Empty, user.Channels[channel.Id]);

        // ensure that our details were updated too
        Assert.AreEqual("different", info.Account);
        Assert.AreEqual("also different", info.RealName);
    }

    [TestMethod]
    public async Task Successfully_ignore_join_for_unjoined_channel()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":foo!~bar@baz/baz JOIN #testing");
        var info = network.AsNetworkInfo();
        var channel = info.GetChannel("#testing");
        var user = info.GetUserByNick("foo");
        Assert.IsNull(channel);
        Assert.IsNull(user);
    }

    [TestMethod]
    [DataRow(":a!~a@a.a PART #testing", DisplayName = "PART without reason")]
    [DataRow(":a!~a@a.a PART #testing,#other", DisplayName = "PART multiple channels")]
    [DataRow(":a!~a@a.a PART #testing :part reason", DisplayName = "PART with reason")]
    [DataRow(":c!~c@c.c KICK #testing a", DisplayName = "KICK without reason")]
    [DataRow(":c!~c@c.c KICK #testing a :kick reason", DisplayName = "KICK with reason")]
    [DataRow(":a!~a@a.a QUIT", DisplayName = "QUIT without reason")]
    [DataRow(":a!~a@a.a QUIT :quit reason", DisplayName = "QUIT with reason")]
    public async Task Successfully_remove_other_from_channel(string line)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        await network.ReceiveLineForUnitTests(":b!~b@b.b JOIN #testing");
        await network.ReceiveLineForUnitTests(":c!~c@c.c JOIN #testing");

        // line should remove a from the channel via various methods
        await network.ReceiveLineForUnitTests(line);
        var info = network.AsNetworkInfo();

        var channel = info.GetChannel("#testing");
        Assert.IsNotNull(channel);
        Assert.IsNull(info.GetUserByNick("a"));
        Assert.HasCount(3, channel.Users);
    }

    [TestMethod]
    [DataRow(":test!id@127.0.0.1 PART #testing", DisplayName = "PART without reason")]
    [DataRow(":test!id@127.0.0.1 PART #testing,#other", DisplayName = "PART multiple channels")]
    [DataRow(":test!id@127.0.0.1 PART #testing :part reason", DisplayName = "PART with reason")]
    [DataRow(":c!~c@c.c KICK #testing test", DisplayName = "KICK without reason")]
    [DataRow(":c!~c@c.c KICK #testing test :kick reason", DisplayName = "KICK with reason")]
    public async Task Successfully_remove_self_from_channel(string line)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        await network.ReceiveLineForUnitTests(":b!~b@b.b JOIN #testing");
        await network.ReceiveLineForUnitTests(":c!~c@c.c JOIN #testing");

        // line should remove a from the channel via various methods
        await network.ReceiveLineForUnitTests(line);
        var info = network.AsNetworkInfo();

        Assert.IsNull(info.GetChannel("#testing"));
        Assert.IsNull(info.GetUserByNick("a"));
    }

    [TestMethod]
    public async Task Successfully_change_modes()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        await network.ReceiveLineForUnitTests(":b!~b@b.b JOIN #testing");
        await network.ReceiveLineForUnitTests(":c!~c@c.c JOIN #testing");

        await network.ReceiveLineForUnitTests(":irc.netwolf.org MODE #testing +iobl a d!*@* 5");
        await network.ReceiveLineForUnitTests(":irc.netwolf.org MODE #testing +vv-vv test a b c");
        var info = network.AsNetworkInfo();

        var channel = info.GetChannel("#testing")!;
        var users = info.GetUsersInChannel(channel);
        var testUser = users.Single(u => u.Key.Nick == "test").Key;
        var aUser = users.Single(u => u.Key.Nick == "a").Key;
        var bUser = users.Single(u => u.Key.Nick == "b").Key;

        Assert.AreEqual("+", users[testUser]);
        Assert.AreEqual("+", channel.Users[testUser.Id]);
        Assert.AreEqual("+", testUser.Channels[channel.Id]);
        Assert.AreEqual("@+", users[aUser]);
        Assert.AreEqual("@+", channel.Users[aUser.Id]);
        Assert.AreEqual("@+", aUser.Channels[channel.Id]);
        Assert.AreEqual(string.Empty, users[bUser]);
        Assert.AreEqual(string.Empty, channel.Users[bUser.Id]);
        Assert.AreEqual(string.Empty, bUser.Channels[channel.Id]);

        await network.ReceiveLineForUnitTests(":irc.netwolf.org MODE #testing +k-o pw a");

        // refresh channel data since records are immutable
        info = network.AsNetworkInfo();
        channel = info.GetChannel("#testing")!;
        users = info.GetUsersInChannel(channel);
        aUser = users.Single(u => u.Key.Nick == "a").Key;

        Assert.AreEqual("+", users[aUser]);
        Assert.AreEqual("+", channel.Users[aUser.Id]);
        Assert.AreEqual("+", aUser.Channels[channel.Id]);
        Assert.AreEqual("ikl", string.Concat(channel.Modes.Keys.OrderBy(c => c)));
        Assert.AreEqual("pw", channel.Modes['k']);
        Assert.AreEqual("5", channel.Modes['l']);
        Assert.IsNull(channel.Modes['i']);
    }

    [TestMethod]
    [DataRow(":a!~a@a.a RENAME #testing #test2 :", "#test2", DisplayName = "RENAME without reason")]
    [DataRow(":irc.netwolf.org RENAME #testing #test2 :", "#test2", DisplayName = "RENAME from server")]
    [DataRow(":a!~a@a.a RENAME #testing #test2 :reason goes here", "#test2", DisplayName = "RENAME with reason")]
    [DataRow(":a!~a@a.a RENAME #testing #TESTING :", "#TESTING", DisplayName = "Change channel case")]
    public async Task Successfully_rename_channel(string line, string newChannel)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":irc.netwolf.org CAP test ACK :draft/channel-rename");
        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(line);
        var info = network.AsNetworkInfo();
        if (!IrcUtil.IrcEquals("#testing", newChannel, CaseMapping.Ascii))
        {
            // only check if the old channel went away if we aren't doing a case change
            Assert.IsNull(info.GetChannel("#testing"));
        }

        var channel = info.GetChannel(newChannel);
        Assert.IsNotNull(channel);
        Assert.AreEqual(newChannel, channel.Name);
    }

    [TestMethod]
    [DataRow(":irc.netwolf.org RENAME #test1 #test2 :", DisplayName = "Not joined to source channel")]
    [DataRow(":irc.netwolf.org RENAME #testing foobar :", DisplayName = "Target isn't a channel")]
    [DataRow(":irc.netwolf.org RENAME test #test2 :", DisplayName = "Source isn't a channel")]
    [DataRow(":irc.netwolf.org RENAME", DisplayName = "Missing all args")]
    [DataRow(":irc.netwolf.org RENAME #testing", DisplayName = "Missing 2nd arg")]
    [DataRow(":irc.netwolf.org RENAME #testing #test2", DisplayName = "Missing 3rd arg")]
    [DataRow(":irc.netwolf.org RENAME #testing :#second test whee", DisplayName = "2nd arg is trailing")]
    public async Task Ignore_invalid_renames(string line)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":irc.netwolf.org CAP test ACK :draft/channel-rename");
        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #another");
        await network.ReceiveLineForUnitTests(line);

        var state = network.AsNetworkInfo();
        Assert.AreEqual("test", state.GetAllUsers().Single().Nick);
        CollectionAssert.AreEquivalent(new List<string> { "#testing", "#another" }, state.GetAllChannels().Select(c => c.Name).ToList());
    }

    [TestMethod]
    [DataRow(":irc.netwolf.org RENAME #another #testing :", DisplayName = "Channel collision")]
    public async Task Throw_on_corrupted_state_renames(string line)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":irc.netwolf.org CAP test ACK :draft/channel-rename");
        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #another");
        await Assert.ThrowsExactlyAsync<BadStateException>(() => network.ReceiveLineForUnitTests(line));
    }

    [TestMethod]
    [DataRow("a", "b", "b", "ascii", false, DisplayName = "Regular nickchange")]
    [DataRow("a", "a^]", "A^]", "ascii", false, DisplayName = "Regular nickchange (casefolded ascii)")]
    [DataRow("a", "a^]", "A~}", "rfc1459", false, DisplayName = "Regular nickchange (casefolded rfc1459)")]
    [DataRow("a", "a^]", "A^}", "rfc1459-strict", false, DisplayName = "Regular nickchange (casefolded rfc1459-strict)")]
    [DataRow("a", "A", "a", "ascii", true, DisplayName = "Casing change")]
    public async Task Successfully_change_nick(string oldNick, string newNick, string lookup, string casemapping, bool casingChange)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests($":irc.netwolf.org 005 test CASEMAPPING={casemapping} :are supported by this server");
        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests($":{oldNick}!id2@host.two JOIN #testing");
        await network.ReceiveLineForUnitTests($":{oldNick}!id2@host.two NICK {newNick}");

        var state = network.AsNetworkInfo();
        var user = state.GetUserByNick(lookup);
        Assert.IsNotNull(user);
        Assert.AreEqual(newNick, user.Nick);
        if (!casingChange)
        {
            Assert.IsNull(state.GetUserByNick(oldNick));
        }
    }

    [TestMethod]
    [DataRow("NICK foo", DisplayName = "Missing source")]
    [DataRow(":irc.netwolf.org NICK foo", DisplayName = "Server as source")]
    [DataRow(":test!id@127.0.0.1 NICK", DisplayName = "Missing argument")]
    [DataRow(":test!id@127.0.0.1 NICK :", DisplayName = "Empty argument")]
    [DataRow(":test!id@127.0.0.1 NICK ::", DisplayName = "Invalid nickname (protocol)")]
    [DataRow(":test!id@127.0.0.1 NICK #testing", DisplayName = "Invalid nickname (channel)")]
    [DataRow(":test!id@127.0.0.1 NICK +foo", DisplayName = "Invalid nickname (status)")]
    [DataRow(":b!~b@b.b NICK c", DisplayName = "Unrecognized source")]
    public async Task Ignore_invalid_nick_changes(string line)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        await network.ReceiveLineForUnitTests(line);

        var state = network.AsNetworkInfo();
        CollectionAssert.AreEquivalent(new List<string> { "test", "a" }, state.GetAllUsers().Select(u => u.Nick).ToList());
        Assert.AreEqual("#testing", state.GetAllChannels().Single().Name);
    }

    [TestMethod]
    [DataRow(":a!~a@a.a NICK test", DisplayName = "Nick collision")]
    public async Task Throw_on_corrupted_state_nick_changes(string line)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        await Assert.ThrowsExactlyAsync<BadStateException>(() => network.ReceiveLineForUnitTests(line));
    }

    [TestMethod]
    public async Task Successfully_self_away_numeric()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":irc.netwolf.org 306 test :You have been marked as being away");
        Assert.IsTrue(network.AsNetworkInfo().IsAway);
    }

    [TestMethod]
    public async Task Successfully_self_unaway_numeric()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        // set up that we're currently away (and validate that setup worked)
        network.UnsafeUpdateUser(network.AsNetworkInfo().Self with { IsAway = true });
        Assert.IsTrue(network.AsNetworkInfo().IsAway);

        await network.ReceiveLineForUnitTests(":irc.netwolf.org 305 test :You are no longer marked as being away");
        Assert.IsFalse(network.AsNetworkInfo().IsAway);
    }

    [TestMethod]
    public async Task Successfully_other_away_numeric()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        await network.ReceiveLineForUnitTests(":irc.netwolf.org 301 test a :Custom away message");
        Assert.IsTrue(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);
    }

    [TestMethod]
    [DataRow("Custom away message", DisplayName = "AWAY with message")]
    [DataRow("", DisplayName = "AWAY without message")]
    public async Task Successfully_other_away_notify(string reason)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        await network.ReceiveLineForUnitTests($":a!~a@a.a AWAY :{reason}");
        Assert.IsTrue(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);
    }

    [TestMethod]
    public async Task Successfully_other_unaway_notify()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");

        // set up that the user is currently away (and validate that setup worked)
        network.UnsafeUpdateUser(network.AsNetworkInfo().GetUserByNick("a")! with { IsAway = true });
        Assert.IsTrue(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);

        await network.ReceiveLineForUnitTests($":a!~a@a.a AWAY");
        Assert.IsFalse(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);
    }

    [TestMethod]
    public async Task Successfully_other_away_who()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        await network.ReceiveLineForUnitTests(":irc.netwolf.org 352 test #testing ~a a.a irc.netwolf.org a G :0 Real Name");
        Assert.IsTrue(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);
    }

    [TestMethod]
    public async Task Successfully_other_unaway_who()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");

        // set up that the user is currently away (and validate that setup worked)
        network.UnsafeUpdateUser(network.AsNetworkInfo().GetUserByNick("a")! with { IsAway = true });
        Assert.IsTrue(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);

        await network.ReceiveLineForUnitTests(":irc.netwolf.org 352 test #testing ~a a.a irc.netwolf.org a H :0 Real Name");
        Assert.IsFalse(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);
    }

    [TestMethod]
    public async Task Successfully_other_away_userhost()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        await network.ReceiveLineForUnitTests(":irc.netwolf.org 302 test :a=-a.a");
        Assert.IsTrue(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);
    }

    [TestMethod]
    public async Task Successfully_other_unaway_userhost()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests("127.0.0.1", "acct");

        await network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        await network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");

        // set up that the user is currently away (and validate that setup worked)
        network.UnsafeUpdateUser(network.AsNetworkInfo().GetUserByNick("a")! with { IsAway = true });
        Assert.IsTrue(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);

        await network.ReceiveLineForUnitTests(":irc.netwolf.org 302 test :a=+a.a");
        Assert.IsFalse(network.AsNetworkInfo().GetUserByNick("a")!.IsAway);
    }
}
