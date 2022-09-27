using Microsoft.Extensions.DependencyInjection;

using Netwolf.Transport.Exceptions;
using Netwolf.Transport.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    public class CommandFactory : ICommandFactory
    {
        public virtual Type ObjectType => typeof(Command);

        // In general, simple regexes that potentially allow more strings than necessary
        // are preferred to strict regexes that perfectly validate things like hostnames
        private static readonly Regex _commandRegex = new("^(?:[a-zA-Z]+|[0-9]{3})$", RegexOptions.Compiled);
        private static readonly Regex _tagKeyRegex = new(@"^\+?(?:[a-zA-Z0-9-.]+/)?[a-zA-Z0-9-]+$", RegexOptions.Compiled);
        private static readonly Regex _spaceNullCrLfRegex = new(@"[ \r\n\0]", RegexOptions.Compiled);
        private static readonly Regex _nullCrLfRegex = new(@"[\r\n\0]", RegexOptions.Compiled);
        private static readonly Regex _parseCommandRegex = new(@"^(?:@(?<tag>[^ ;]+)(?:;(?<tag>[^ ;]+))* +)?(?::(?<source>[^ ]+) +)?(?<verb>[^ ]+)(?: +(?<arg>[^: ][^ ]*))*(?: +:(?<trailing>.*))?\r\n$", RegexOptions.Compiled);

        private IServiceProvider Provider { get; init; }

        public CommandFactory(IServiceProvider provider)
        {
            Provider = provider;
        }

        public ICommand CreateCommand(CommandType commandType, string? source, string verb, IReadOnlyList<string?> args, IReadOnlyDictionary<string, string?> tags)
        {
            // Verify parameters
            ArgumentNullException.ThrowIfNull(verb);
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(tags);

            List<string> commandArgs = new();
            Dictionary<string, string?> commandTags = new();

            // source could be a hostmask or DNS name, and sometimes cute things happen such as embedded color codes.
            // Simply forbid known-invalid characters rather than attempting to write strict validation
            if (source != null && _spaceNullCrLfRegex.IsMatch(source))
            {
                throw new ArgumentException("Invalid source", nameof(source));
            }

            if (!_commandRegex.IsMatch(verb))
            {
                throw new ArgumentException("Invalid command verb", nameof(verb));
            }

            var allowedTagLength = commandType switch
            {
                CommandType.Server => 8191,
                CommandType.Client => 4096,
                _ => throw new ArgumentException("Invalid command type", nameof(commandType))
            };

            // normalize verb to all-uppercase
            verb = verb.ToUpperInvariant();

            bool hasTrailingArg = false;
            for (int i = 0; i < args.Count; ++i)
            {
                var arg = args[i];
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
                    if (i == args.Count - 1)
                    {
                        hasTrailingArg = true;
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid trailing argument at position {i}", nameof(args));
                    }
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
                commandTags[key] = (value == string.Empty) ? null : value;
            }

            var commandOptions = new CommandOptions(commandType, source, verb, commandArgs, commandTags, hasTrailingArg);
            var command = (ICommand)ActivatorUtilities.CreateInstance(Provider, ObjectType, commandOptions);
            if (command.PrefixedCommandPart.EncodeUtf8().Length > 512)
            {
                throw new CommandTooLongException($"Command is too long, {command.PrefixedCommandPart.Length} bytes found but 512 bytes allowed.");
            }

            if (command.TagPart.EncodeUtf8().Length > allowedTagLength)
            {
                throw new CommandTooLongException($"Tags are too long, {command.TagPart.Length} bytes found but {allowedTagLength} bytes allowed.");
            }

            return command;
        }

        public ICommand Parse(CommandType commandType, string message)
        {
            var matches = _parseCommandRegex.Match(message);
            if (!matches.Success)
            {
                throw new ArgumentException("Invalid or ill-formed IRC message", nameof(message));
            }

            var verb = matches.Groups["verb"].Value;
            string? source = null;
            Dictionary<string, string?> tags = new();
            List<string> args = new();

            if (matches.Groups.ContainsKey("source"))
            {
                source = matches.Groups["source"].Value;
            }

            if (matches.Groups.ContainsKey("tag"))
            {
                foreach (var tag in matches.Groups["tag"].Captures.Cast<Capture>())
                {
                    var parts = tag.Value.Split(new char[] { '=' }, 2);
                    if (parts.Length == 1 || parts[1] == string.Empty)
                    {
                        tags[parts[0]] = null;
                    }
                    else
                    {
                        // unescape the tag value
                        var sb = new StringBuilder();
                        var escape = false;
                        foreach (var c in parts[1])
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

            if (matches.Groups.ContainsKey("arg"))
            {
                foreach (var arg in matches.Groups["arg"].Captures.Cast<Capture>())
                {
                    args.Add(arg.Value);
                }
            }

            if (matches.Groups.ContainsKey("trailing"))
            {
                args.Add(matches.Groups["trailing"].Value);
            }

            return CreateCommand(commandType, source, verb, args, tags);
        }
    }
}
