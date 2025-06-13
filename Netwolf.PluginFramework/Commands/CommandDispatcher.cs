// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Context;
using Netwolf.PluginFramework.Exceptions;
using Netwolf.PluginFramework.Permissions;
using Netwolf.Transport.IRC;

using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Netwolf.PluginFramework.Commands;

public partial class CommandDispatcher<TResult> : ICommandDispatcher<TResult>
{
    [GeneratedRegex("^[A-Z][A-Z0-9]*$")]
    private static partial Regex CommandNameRegex();

    private ILogger Logger { get; init; }

    private IServiceProvider ServiceProvider { get; init; }

    private ICommandHookRegistry HookRegistry { get; init; }

    private ICommandValidator<TResult> Validator { get; init; }

    private IEnumerable<IPermissionManager> PermissionManagers { get; init; }

    private IEnumerable<IContextAugmenter> Augmenters { get; init; }

    private Dictionary<string, ICommandHandler<TResult>> Commands { get; init; } = [];

    // FIXME: once we're on .NET 10, make use of CompareOptions.NumericOrdering for natural sort order
    ImmutableArray<string> ICommandDispatcher<TResult>.Commands =>
        [.. Commands.Keys.Union(HookRegistry.GetHookedCommandNames()).Order()];

    public CommandDispatcher(
        IServiceProvider provider,
        ILogger<ICommandDispatcher<TResult>> logger,
        ICommandHookRegistry hookRegistry,
        ICommandValidator<TResult> validator,
        IEnumerable<IPermissionManager> permissionManagers,
        IEnumerable<IContextAugmenter> augmenters
        )
    {
        ServiceProvider = provider;
        Logger = logger;
        HookRegistry = hookRegistry;
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

    public ICommandHandler<TResult>? AddCommand<TCommand>()
        where TCommand : ICommandHandler<TResult>
    {
        return AddCommand(typeof(TCommand)) as ICommandHandler<TResult>;
    }

    public ICommandHandler? AddCommand(Type commandType)
    {
        if (!commandType.IsAssignableTo(typeof(ICommandHandler<TResult>)) || commandType.IsAbstract || commandType.IsInterface)
        {
            Logger.LogTrace(@"Skipping {Type}: is abstract, interface, or not assignable to command handler type", commandType.FullName);
            return null;
        }

        if (commandType.ContainsGenericParameters)
        {
            // warn on non-abstract open generics since this indicates a likely programmer mistake
            Logger.LogWarning(@"Skipping {Type}: open generic types are not supported", commandType.FullName);
            return null;
        }

        if (!Validator.ValidateCommandType(commandType))
        {
            Logger.LogDebug(@"Skipping {Type}: type validator returned false", commandType.FullName);
            return null;
        }

        // now that we've gotten this far, instantiate it
        ICommandHandler<TResult> handler = (ICommandHandler<TResult>)ActivatorUtilities.CreateInstance(ServiceProvider, commandType);
        return AddCommand(handler) ? handler : null;
    }

    public bool AddCommand(ICommandHandler<TResult> handler)
    {
        var commandType = handler.GetType();
        if (!Validator.ValidateCommandType(commandType))
        {
            Logger.LogDebug(@"Skipping {Type}: type validator returned false", commandType.FullName);
            return false;
        }

        if (!CommandNameRegex().IsMatch(handler.Command))
        {
            Logger.LogWarning(@"Skipping {Underlying}: bad command name {Command}", handler.UnderlyingFullName, handler.Command);
            return false;
        }

        if (!Validator.ValidateCommandHandler(handler))
        {
            Logger.LogDebug(@"Skipping {Underlying}: handler validator returned false", handler.UnderlyingFullName);
            return false;
        }

        Commands.Add(handler.Command, handler);
        Logger.LogInformation("Found {Underlying} providing {Command}", handler.UnderlyingFullName, handler.Command);
        return true;
    }

    public bool RemoveCommand(ICommandHandler handler)
    {
        if (!Commands.TryGetValue(handler.Command, out var existing))
        {
            // command doesn't exist, treat removal as successful (idempotency)
            Logger.LogDebug("Attempting to remove handler for non-existent command {Command}", handler.Command);
            return true;
        }

        if (ReferenceEquals(handler, existing))
        {
            Logger.LogDebug("Removed handler {Underlying} for command {Command}", handler.UnderlyingFullName, handler.Command);
            Commands.Remove(handler.Command);
            return true;
        }
        else
        {
            Logger.LogWarning(
                "Attempted to remove handler {Underlying} for command {Command} but command is registered to {Existing} instead",
                handler.UnderlyingFullName,
                handler.Command,
                existing.UnderlyingFullName);
            return false;
        }
    }

    public async Task<TResult?> DispatchAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hooks = HookRegistry.GetCommandHooks(command.Verb).ToList();

        if (!Commands.TryGetValue(command.Verb, out var handler) && hooks.Count == 0)
        {
            Logger.LogDebug("Received unknown command {Command}", command.UnprefixedCommandPart);
            return default;
        }

        handler ??= new DummyHandler<TResult>(command.Verb, Logger);

        foreach (var augmenter in Augmenters)
        {
            augmenter.AugmentForCommand(sender, command, handler);
        }

        sender.FreezeExtensionData();
        Validator.ValidateCommand(command, handler, sender);

        if (handler.Privilege != null)
        {
            IPermissionManager? selectedManager = null;
            bool hasPermission = false;

            foreach (var manager in PermissionManagers)
            {
                Logger.LogTrace("Querying {Manager} to see if context {Type} has permission {Permission} for {Command}",
                    manager.GetType().FullName,
                    sender.GetType().FullName,
                    handler.Privilege,
                    command.Verb);

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

        foreach (var hook in hooks)
        {
            Logger.LogTrace("Executing hook {Hook} for command {Command}", hook.UnderlyingFullName, command.Verb);
            var result = await hook.ExecuteAsync(command, sender, cancellationToken);
            if (result.HasFlag(PluginResult.SuppressDefault))
            {
                Logger.LogTrace("Hook {Hook} requested default command suppression for {Command}", hook.UnderlyingFullName, command.Verb);
                handler = new DummyHandler<TResult>(command.Verb, null);
            }

            if (result.HasFlag(PluginResult.SuppressPlugins))
            {
                Logger.LogTrace("Hook {Hook} requested plugin suppression for {Command}", hook.UnderlyingFullName, command.Verb);
                break;
            }
        }

        return await handler.ExecuteAsync(command, sender, cancellationToken)!;
    }
}
