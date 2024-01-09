using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Server.Internal;
using Netwolf.Transport.IRC;

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Netwolf.Server.Commands;

internal partial class CommandDispatcher : ICommandDispatcher
{
    [GeneratedRegex("^[A-Z][A-Z0-9]*$")]
    private static partial Regex CommandNameRegex();

    private ILogger Logger { get; init; }

    private Dictionary<string, ICommandHandler> Commands { get; init; } = [];

    public CommandDispatcher(IServiceProvider provider, ILogger<ICommandDispatcher> logger, IOptionsSnapshot<ServerOptions> options)
    {
        Logger = logger;

        // populate Commands from all concrete classes across all assemblies that implement ICommandHandler
        logger.LogTrace("Scanning for commands");
        foreach (var handler in TypeDiscovery.GetTypes<ICommandHandler>(provider, options))
        {
            var type = handler.GetType();

            if (!CommandNameRegex().IsMatch(handler.Command))
            {
                logger.LogWarning(@"Skipping {Type}: bad command name {Command}", type.FullName, handler.Command);
                continue;
            }

            if (handler.Privilege != null)
            {
                if (handler.Privilege.Length < 6 || handler.Privilege[5] != ':')
                {
                    logger.LogWarning(@"Skipping {Type}: invalid privilege {Privilege}", type.FullName, handler.Privilege);
                    continue;
                }

                var container = handler.Privilege.AsSpan()[..4];
                switch (container)
                {
                    case "user":
                    case "oper":
                        break;
                    case "chan":
                        // For commands that require channel privileges, a channel must be the first parameter
                        // and it must not be optional, repeated, or a list
                        if (!handler.HasChannel)
                        {
                            logger.LogWarning(@"Skipping {Type}: channel privilege specified but command lacks a channel", type.FullName);
                            continue;
                        }

                        break;
                    default:
                        logger.LogWarning(@"Skipping {Type}: invalid privilege scope", type.FullName);
                        continue;
                }
            }

            Commands.Add(handler.Command, handler);
            logger.LogTrace("Found {Type} providing {Command}", type.FullName, handler.Command);
        }
    }

    public Task<ICommandResponse> DispatchAsync(ICommand command, User client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.CommandType != CommandType.Client)
        {
            throw new ArgumentException("Not passed a client command", nameof(command));
        }

        if (!Commands.TryGetValue(command.Verb, out var handler))
        {
            Logger.LogDebug("Received unknown command {Command}", command.UnprefixedCommandPart);
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_UNKNOWNCOMMAND, command.Verb));
        }

        Channel? channel = null;
        if (handler.HasChannel)
        {
            // Convert the first parameter to a channel
        }

        if (handler.Privilege != null && !client.HasPrivilege(handler.Privilege, channel))
        {
            // Error message differs based on what type of privilege is missing,
            // and perhaps other context (e.g. oper commands have a different error if the user is an oper vs not)
        }

        return handler.ExecuteAsync(command, client, channel, cancellationToken);
    }
}
