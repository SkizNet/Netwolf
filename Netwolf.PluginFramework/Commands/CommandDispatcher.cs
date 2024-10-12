using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.PluginFramework.Context;
using Netwolf.PluginFramework.Exceptions;
using Netwolf.PluginFramework.Permissions;

using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Netwolf.PluginFramework.Commands;

public partial class CommandDispatcher<TResult> : ICommandDispatcher<TResult>
{
    [GeneratedRegex("^[A-Z][A-Z0-9]*$")]
    private static partial Regex CommandNameRegex();

    private ILogger Logger { get; init; }

    private IServiceProvider ServiceProvider { get; init; }

    private ICommandValidator<TResult> Validator { get; init; }

    private IEnumerable<IPermissionManager> PermissionManagers { get; init; }

    private IEnumerable<IContextAugmenter> Augmenters { get; init; }

    private Dictionary<string, ICommandHandler<TResult>> Commands { get; init; } = [];

    ImmutableArray<string> ICommandDispatcher<TResult>.Commands => [.. Commands.Keys];

    public CommandDispatcher(
        IServiceProvider provider,
        ILogger<ICommandDispatcher<TResult>> logger,
        ICommandValidator<TResult> validator,
        IEnumerable<IPermissionManager> permissionManagers,
        IEnumerable<IContextAugmenter> augmenters
        )
    {
        ServiceProvider = provider;
        Logger = logger;
        Validator = validator;
        PermissionManagers = permissionManagers;
        Augmenters = augmenters;
    }

    public void AddCommandsFromAssembly(Assembly assembly)
    {
        // populate Commands from all concrete public classes across the assembly that implement ICommandHandler
        Logger.LogTrace("Scanning for commands in {Name}", assembly.FullName);
        foreach (var type in assembly.ExportedTypes)
        {
            AddCommand(type);
        }
    }

    public void AddCommand<TCommand>()
        where TCommand : ICommandHandler<TResult>
    {
        AddCommand(typeof(TCommand));
    }

    public void AddCommand(Type commandType)
    {
        if (!commandType.IsAssignableTo(typeof(ICommandHandler<TResult>)) || commandType.IsAbstract || commandType.IsInterface)
        {
            Logger.LogTrace(@"Skipping {Type}: is abstract, interface, or not assignable to command handler type", commandType.FullName);
            return;
        }

        if (commandType.ContainsGenericParameters)
        {
            // warn on non-abstract open generics since this indicates a likely programmer mistake
            Logger.LogWarning(@"Skipping {Type}: open generic types are not supported", commandType.FullName);
            return;
        }

        if (!Validator.ValidateCommandType(commandType))
        {
            Logger.LogDebug(@"Skipping {Type}: type validator returned false", commandType.FullName);
            return;
        }

        // now that we've gotten this far, instantiate it
        ICommandHandler<TResult> handler = (ICommandHandler<TResult>)ActivatorUtilities.CreateInstance(ServiceProvider, commandType);

        if (!CommandNameRegex().IsMatch(handler.Command))
        {
            Logger.LogWarning(@"Skipping {Underlying}: bad command name {Command}", handler.UnderlyingFullName, handler.Command);
            return;
        }

        if (!Validator.ValidateCommandHandler(handler))
        {
            Logger.LogDebug(@"Skipping {Underlying}: handler validator returned false", handler.UnderlyingFullName);
            return;
        }

        Commands.Add(handler.Command, handler);
        Logger.LogInformation("Found {Underlying} providing {Command}", handler.UnderlyingFullName, handler.Command);
    }

    public void AddCommand(ICommandHandler<TResult> handler)
    {
        var commandType = handler.GetType();
        if (!Validator.ValidateCommandType(commandType))
        {
            Logger.LogDebug(@"Skipping {Type}: type validator returned false", commandType.FullName);
            return;
        }

        if (!CommandNameRegex().IsMatch(handler.Command))
        {
            Logger.LogWarning(@"Skipping {Underlying}: bad command name {Command}", handler.UnderlyingFullName, handler.Command);
            return;
        }

        if (!Validator.ValidateCommandHandler(handler))
        {
            Logger.LogDebug(@"Skipping {Underlying}: handler validator returned false", handler.UnderlyingFullName);
            return;
        }

        Commands.Add(handler.Command, handler);
        Logger.LogInformation("Found {Underlying} providing {Command}", handler.UnderlyingFullName, handler.Command);
    }

    public Task<TResult?> DispatchAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Commands.TryGetValue(command.Verb, out var handler))
        {
            Logger.LogDebug("Received unknown command {Command}", command.UnprefixedCommandPart);
            return Task.FromResult<TResult?>(default);
        }

        foreach (var augmenter in Augmenters)
        {
            sender = augmenter.AugmentForCommand(sender, command, handler);
        }

        Validator.ValidateCommand(command, handler, sender);

        if (handler.Privilege != null)
        {
            IPermissionManager? selectedManager = null;
            bool hasPermission = false;

            foreach (var manager in PermissionManagers)
            {
                try
                {
                    hasPermission = manager.HasPermission(sender, handler.Privilege);
                    // HasPermission will throw NotImplementedException if it doesn't support this combo,
                    // so if it proceeds to these lines we found a supported manager, so use it.
                    selectedManager = manager;
                    break;
                }
                catch (NotImplementedException) { /* swallow */ }
            }

            if (selectedManager == null)
            {
                Logger.LogError("No registered permission managers support the given context type {Type} and permission {Permission} for {Command}",
                    sender.GetType().FullName,
                    handler.Privilege,
                    command.UnprefixedCommandPart);

                throw new NoMatchingPermissionManagerException(command.Verb, sender.GetType(), handler.Privilege);
            }

            if (!hasPermission)
            {
                throw selectedManager.GetPermissionError(sender, handler.Privilege);
            }
        }

        return handler.ExecuteAsync(command, sender, cancellationToken)!;
    }
}
