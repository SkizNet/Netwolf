// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Exceptions;
using Netwolf.Transport.State;
using Netwolf.Unicode;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Netwolf.Transport.IRC;

public partial class CommandFactory : ICommandFactory
{
    public virtual Type ObjectType => typeof(Command);

    // In general, simple regexes that potentially allow more strings than necessary
    // are preferred to strict regexes that perfectly validate things like hostnames
    private static readonly Regex _commandRegex = new("^(?:[a-zA-Z]+|[0-9]{3})$", RegexOptions.NonBacktracking);
    // this is probably *too* strict on the other hand, because it doesn't provide support for bot commands in languages that do not use the English alphabet
    // however it's easier to expand on allowed commands later versus restricting them so for now we go with this
    // bot commands are meant to be case-sensitive so expanding this to allow more symbols will require culture-sensitive comparisons
    private static readonly Regex _botCommandRegex = new("^(?:[a-zA-Z0-9_]+)$", RegexOptions.NonBacktracking);
    private static readonly Regex _tagKeyRegex = new(@"^\+?(?:[a-zA-Z0-9-.]+/)?[a-zA-Z0-9-]+$", RegexOptions.NonBacktracking);
    private static readonly Regex _spaceNullCrLfRegex = new(@"[ \r\n\0]", RegexOptions.NonBacktracking);
    private static readonly Regex _nullCrLfRegex = new(@"[\r\n\0]", RegexOptions.NonBacktracking);

    // cannot be NonBacktracking, as NonBacktracking does not properly capture a named group multiple times (only returns the last match)
    [GeneratedRegex("^(?:@(?<tag>[^ ;]+)(?:;(?<tag>[^ ;]+))* +)?(?::(?<source>[^ ]+) +)?(?<verb>[^ ]+)(?: +(?<arg>[^: ][^ ]*))*(?: +(?::(?<trailing>.*))?)?$")]
    private static partial Regex ParseCommandRegex();

    private IServiceProvider Provider { get; init; }

    public CommandFactory(IServiceProvider provider)
    {
        Provider = provider;
    }

    public ICommand CreateCommand(
        CommandType commandType,
        string? source,
        string verb,
        IReadOnlyList<string?> args,
        IReadOnlyDictionary<string, string?> tags,
        CommandCreationOptions? options = null)
    {
        // Verify parameters
        ArgumentNullException.ThrowIfNull(verb);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(tags);

        List<string> commandArgs = [];
        Dictionary<string, string?> commandTags = [];

        options ??= new();
        if (options.LineLen < 512 || options.ClientTagLen < 4096 || options.ServerTagLen < 8191)
        {
            throw new ArgumentException("Options contains invalid length limits; limits can only be increased above RFC limits, not reduced", nameof(options));
        }

        // source could be a hostmask or DNS name, and sometimes cute things happen such as embedded color codes.
        // Simply forbid known-invalid characters rather than attempting to write strict validation
        if (source != null && _spaceNullCrLfRegex.IsMatch(source))
        {
            throw new ArgumentException($"Invalid source {source}", nameof(source));
        }

        if (commandType != CommandType.Bot && !_commandRegex.IsMatch(verb))
        {
            throw new ArgumentException($"Invalid command verb {verb}", nameof(verb));
        }
        else if (commandType == CommandType.Bot && !_botCommandRegex.IsMatch(verb))
        {
            throw new ArgumentException($"Invalid command verb {verb}", nameof(verb));
        }

        int allowedTagLength = commandType switch
        {
            CommandType.Server => options.ServerTagLen,
            CommandType.Client => options.ClientTagLen,
            CommandType.Bot => options.ClientTagLen,
            _ => throw new ArgumentException($"Invalid command type {commandType}", nameof(commandType))
        };

        // normalize verb to all-uppercase
        verb = verb.ToUpperInvariant();

        bool hasTrailingArg = false;
        for (int i = 0; i < args.Count; ++i)
        {
            string? arg = args[i];
            if (arg == null)
            {
                continue;
            }

            if (_nullCrLfRegex.IsMatch(arg))
            {
                throw new ArgumentException($"Invalid characters in argument at position {i}", nameof(args));
            }

            if (arg == string.Empty || arg[0] == ':' || arg.Contains(' '))
            {
                hasTrailingArg = i == args.Count - 1 ? true : throw new ArgumentException($"Invalid trailing argument at position {i}", nameof(args));
            }

            commandArgs.Add(arg);
        }

        foreach (var (key, value) in tags)
        {
            if (!_tagKeyRegex.IsMatch(key))
            {
                throw new ArgumentException("Invalid tag key", nameof(tags));
            }

            // Note: there is no tag value validation, because at this point tag values are unescaped
            // (escaping is done when sending and unescaping when receiving), so the only requirement is that
            // tag values are valid UTF-8 (which was also validated already since the bytes were decoded to a string by now).

            // normalize empty string values to null for consistency (allowed by spec)
            commandTags[key] = value == string.Empty ? null : value;
        }

        var commandOptions = new CommandOptions(commandType, source, verb, commandArgs, commandTags, hasTrailingArg);
        var command = (ICommand)ActivatorUtilities.CreateInstance(Provider, ObjectType, commandOptions);

        // PrefixedCommandPart doesn't include trailing CRLF, so subtract 2 from our maximal length to account for that protocol overhead
        int allowedLineLength = options.LineLen - 2;
        if (command.PrefixedCommandPart.EncodeUtf8().Length > allowedLineLength)
        {
            throw new CommandTooLongException($"Command is too long, {command.PrefixedCommandPart.Length} bytes found but {allowedLineLength} bytes allowed.");
        }

        if (command.TagPart.EncodeUtf8().Length > allowedTagLength)
        {
            throw new CommandTooLongException($"Tags are too long, {command.TagPart.Length} bytes found but {allowedTagLength} bytes allowed.");
        }

        return command;
    }

    public ICommand Parse(CommandType commandType, string message)
    {
        var matches = ParseCommandRegex().Match(message);
        if (!matches.Success)
        {
            throw new ArgumentException("Invalid or ill-formed IRC message", nameof(message));
        }

        string verb = matches.Groups["verb"].Value;
        string? source = null;
        Dictionary<string, string?> tags = [];
        List<string> args = [];

        if (matches.Groups["source"].Success)
        {
            source = matches.Groups["source"].Value;
        }

        if (matches.Groups["tag"].Success)
        {
            foreach (var tag in matches.Groups["tag"].Captures.Cast<Capture>())
            {
                string[] parts = tag.Value.Split(['='], 2);
                if (parts.Length == 1 || parts[1] == string.Empty)
                {
                    tags[parts[0]] = null;
                }
                else
                {
                    // unescape the tag value
                    var sb = new StringBuilder();
                    bool escape = false;
                    foreach (char c in parts[1])
                    {
                        if (escape == false && c == '\\')
                        {
                            escape = true;
                            continue;
                        }

                        if (escape)
                        {
                            sb.Append(c switch
                            {
                                ':' => ';',
                                's' => ' ',
                                'r' => '\r',
                                'n' => '\n',
                                _ => c
                            });

                            escape = false;
                        }
                        else
                        {
                            sb.Append(c);
                        }
                    }

                    tags[parts[0]] = sb.ToString();
                }
            }
        }

        if (matches.Groups["arg"].Success)
        {
            foreach (var arg in matches.Groups["arg"].Captures.Cast<Capture>())
            {
                args.Add(arg.Value);
            }
        }

        if (matches.Groups["trailing"].Success)
        {
            args.Add(matches.Groups["trailing"].Value);
        }

        return CreateCommand(commandType, source, verb, args, tags);
    }

    /// <inheritdoc />
    [SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "ToList() is more semantically meaningful")]
    public ICommand PrepareClientCommand(
        UserRecord sender,
        string verb,
        IEnumerable<object?>? args = null,
        IReadOnlyDictionary<string, string?>? tags = null,
        CommandCreationOptions? options = null)
    {
        return CreateCommand(
            CommandType.Client,
            sender.Hostmask,
            verb,
            (args ?? []).Select(o => o?.ToString()).Where(o => o != null).ToList(),
            (tags ?? new Dictionary<string, string?>()).ToDictionary(),
            options);
    }

    /// <inheritdoc />
    public ICommand[] PrepareClientMessage(
        UserRecord sender,
        MessageType messageType,
        string target,
        string text,
        IReadOnlyDictionary<string, string?>? tags = null,
        string? sharedChannel = null,
        CommandCreationOptions? options = null)
    {
        var commands = new List<ICommand>();
        options ??= new();

        string verb = messageType switch
        {
            MessageType.Message => "PRIVMSG",
            MessageType.Notice => "NOTICE",
            _ => throw new ArgumentException("Invalid message type", nameof(messageType))
        };

        List<string> args = [target];

        // :<hostmask> <verb> <target> :<text>\r\n -- 2 colons + 3 spaces + CRLF = 7 syntax characters. If CPRIVMSG/CNOTICE, one extra space is needed.
        // we build in an additional safety buffer of 14 bytes to account for cases where our hostmask is out of sync or the server adds additional context
        // to relayed messages (for 7 + 14 = 21 total bytes, leaving 491 for the rest normally or 490 when using CPRIVMSG/CNOTICE)
        int maxlen = options.LineLen - 21 - sender.Hostmask.Length - verb.Length - target.Length;

        // If CPRIVMSG/CNOTICE is enabled by the ircd and a sharedChannel was given, use it
        if (sharedChannel != null && ((messageType == MessageType.Message && options.UseCPrivMsg) || (messageType == MessageType.Notice && options.UseCNotice)))
        {
            verb = "C" + verb;
            maxlen -= 1 + sharedChannel.Length;
            args.Add(sharedChannel);
        }

        // we overwrite the final value in args with each line of text
        // however List doesn't let us create *new* indexes using the indexer, so add a placeholder for the time being
        int lineIndex = args.Count;
        args.Add(null!);

        // split text if it is longer than maxlen bytes
        bool multilineEnabled = options.UseDraftMultiLine;

        // did the server give us stupid limits? if so disable multiline
        if (options.MultiLineMaxBytes <= maxlen || options.MultiLineMaxLines <= 1)
        {
            multilineEnabled = false;
        }

        var lines = LineBreakHelper.SplitText(text, maxlen, false);

        string batchId = Guid.NewGuid().ToString();
        int batchLines = 0;
        int batchBytes = -1; // our algorithm incorrectly credits an additional byte for a \n before the first line of the batch since isConcat is false for the first line
        bool isConcat = false;
        ImmutableDictionary<string, string?> tagsBase = tags != null ? ImmutableDictionary.CreateRange(tags) : ImmutableDictionary<string, string?>.Empty;
        ImmutableDictionary<string, string?> tagsFinal = tagsBase;

        foreach (var (line, isHardBreak) in lines)
        {
            if (multilineEnabled)
            {
                int byteCount = Encoding.UTF8.GetByteCount(args[^1]) + (isConcat ? 0 : 1);

                // do we need to start a new batch?
                if (batchLines == 0 || batchLines + 1 > options.MultiLineMaxLines || batchBytes + byteCount > options.MultiLineMaxBytes)
                {
                    // do we need to end a previous batch?
                    if (batchLines != 0)
                    {
                        commands.Add(CreateCommand(CommandType.Client, sender.Hostmask, "BATCH", [$"-{batchId}"], tagsBase, options));
                        batchId = Guid.NewGuid().ToString();
                        batchLines = 0;
                        batchBytes = -1; // see previous comment on batchBytes for why we start at -1
                    }

                    commands.Add(CreateCommand(CommandType.Client, sender.Hostmask, "BATCH", [$"+{batchId}", "draft/multiline", target], tagsBase, options));
                    isConcat = false;
                }

                tagsFinal = tagsBase.SetItem("batch", batchId);
                if (isConcat)
                {
                    tagsFinal = tagsFinal.SetItem("draft/multiline-concat", null);
                }
                else
                {
                    tagsFinal = tagsFinal.Remove("draft/multiline-concat");
                }

                batchLines++;
                batchBytes += byteCount;
                isConcat = !isHardBreak;
            }

            args[lineIndex] = line;
            commands.Add(CreateCommand(CommandType.Client, sender.Hostmask, verb, args, tagsFinal, options));
        }

        if (multilineEnabled)
        {
            // end the final batch
            commands.Add(CreateCommand(CommandType.Client, sender.Hostmask, "BATCH", [$"-{batchId}"], tagsBase, options));
        }

        return [.. commands];
    }
}
