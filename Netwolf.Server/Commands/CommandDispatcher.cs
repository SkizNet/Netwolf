using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Netwolf.Transport.Client;

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Netwolf.Server.Commands;

internal class CommandDispatcher : ICommandDispatcher
{
    private ILogger Logger { get; init; }

    private Dictionary<string, ICommandHandler> Commands { get; init; } = new();

    public CommandDispatcher(IServiceProvider provider, ILogger<ICommandDispatcher> logger)
    {
        Logger = logger;

        // populate Commands from all concrete classes across all assemblies that implement ICommandHandler
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            logger.LogTrace("Scanning {Assembly} for commands", assembly.FullName);

            foreach (var type in assembly.ExportedTypes)
            {
                if (type.IsAbstract || !type.IsAssignableTo(typeof(ICommandHandler)))
                {
                    continue;
                }

                var handler = (ICommandHandler)ActivatorUtilities.CreateInstance(provider, type);
                if (!Regex.IsMatch("^[A-Z][A-Z0-9]*$", handler.Command))
                {
                    logger.LogWarning(@"Skipping {Type}: bad command name", type.FullName);
                    continue;
                }

                if (handler.Privilege != null)
                {
                    if (handler.Privilege.Length < 6 || handler.Privilege[5] != ':')
                    {
                        logger.LogWarning(@"Skipping {Type}: invalid privilege", type.FullName);
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
            throw new NotImplementedException();
        }

        Channel? channel = null;
        if (handler.HasChannel)
        {
            // Convert the first parameter to a channel
        }

        if (handler.Privilege != null)
        {

        }

        return handler.ExecuteAsync(command, client, channel, cancellationToken);
    }
}
