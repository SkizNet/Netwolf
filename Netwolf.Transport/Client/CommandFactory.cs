using Microsoft.Extensions.DependencyInjection;

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
        public virtual Type CommandType => typeof(Command);

        // In general, simple regexes that potentially allow more strings than necessary
        // are preferred to strict regexes that perfectly validate things like hostnames
        private static readonly Regex _commandRegex = new("^(?:[a-zA-Z]+|[0-9]{3})$", RegexOptions.Compiled);
        private static readonly Regex _tagKeyRegex = new(@"^\+?(?:[a-zA-Z0-9-.]+/)?[a-zA-Z0-9-.]+$", RegexOptions.Compiled);
        private static readonly Regex _spaceNullCrLfRegex = new(@"[ \r\n\0]", RegexOptions.Compiled);
        private static readonly Regex _nullCrLfRegex = new(@"[\r\n\0]", RegexOptions.Compiled);

        private IServiceProvider Provider { get; init; }

        public CommandFactory(IServiceProvider provider)
        {
            Provider = provider;
        }

        public ICommand CreateCommand(CommandType commandType, string? source, string verb, List<string> args, Dictionary<string, string?> tags)
        {
            // Verify parameters
            ArgumentNullException.ThrowIfNull(verb);
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(tags);

            // source could be a hostmask or DNS name, and sometimes cute things happen such as embedded color codes.
            // Simply forbid known-invalid characters rather than attempting to write strict validation
            if (source != null && _spaceNullCrLfRegex.IsMatch(source))
            {
                throw new ArgumentException("Invalid source", nameof(source));
            }

            // enforce null source when sending a command from the client to a server
            if (commandType == Client.CommandType.Client)
            {
                source = null;
            }

            if (!_commandRegex.IsMatch(verb))
            {
                throw new ArgumentException("Invalid command verb", nameof(verb));
            }

            // normalize verb to all-uppercase
            verb = verb.ToUpperInvariant();

            bool hasTrailingArg = false;
            for (int i = 0; i < args.Count; ++i)
            {
                if (args[i] == null)
                {
                    throw new ArgumentException("Individual args cannot be null", nameof(args));
                }

                if (_nullCrLfRegex.IsMatch(args[i]))
                {
                    throw new ArgumentException($"Invalid characters in argument at position {i}", nameof(args));
                }

                if (args[i] == "" || args[i][0] == ':' || args[i].Contains(' '))
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
                if (value == "")
                {
                    tags[key] = null;
                }
            }

            var commandOptions = new CommandOptions(commandType, source, verb, args, tags, hasTrailingArg);

            return (ICommand)ActivatorUtilities.CreateInstance(Provider, CommandType, commandOptions);
        }
    }
}
