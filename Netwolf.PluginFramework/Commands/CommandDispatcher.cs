// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Context;
using Netwolf.PluginFramework.Exceptions;
using Netwolf.PluginFramework.Permissions;

using System.Collections.Immutable;
using System.Reactive.Disposables;
using System.Reflection;
using System.Text.RegularExpressions;

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

    private Dictionary<string, List<ICommandHandler<PluginResult>>> CommandHooks { get; init; } = [];

    ImmutableArray<string> ICommandDispatcher<TResult>.Commands =>
        [.. Commands.Keys.Union(CommandHooks.Keys).Order()];

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

    public IDisposable AddCommandHook(ICommandHandler<PluginResult> handler)
    {
        if (!CommandNameRegex().IsMatch(handler.Command))
        {
            Logger.LogError(
                "Attempted to add command hook {Underlying} for command {Command} but command name is invalid",
                handler.UnderlyingFullName,
                handler.Command);

            throw new InvalidOperationException($"Cannot add command hook {handler.UnderlyingFullName}: bad command name {handler.Command}");
        }

        if (!CommandHooks.TryGetValue(handler.Command, out var hooks))
        {
            hooks = [];
            CommandHooks[handler.Command] = hooks;
        }

        if (hooks.Contains(handler))
        {
            Logger.LogError(
                "Attempted to add command {Underlying} for command {Command} but it is already registered",
                handler.UnderlyingFullName,
                handler.Command);

            throw new InvalidOperationException($"Cannot add command hook {handler.UnderlyingFullName}: hook already registered");
        }

        hooks.Add(handler);
        Logger.LogInformation("Added command hook {Underlying} for command {Command}", handler.UnderlyingFullName, handler.Command);
        return Disposable.Create(() => RemoveCommandHook(handler));
    }

    private void RemoveCommandHook(ICommandHandler<PluginResult> handler)
    {
        if (CommandHooks.TryGetValue(handler.Command, out var hooks) && hooks.Remove(handler))
        {
            Logger.LogInformation("Removed command hook {Underlying} for command {Command}", handler.UnderlyingFullName, handler.Command);

            if (hooks.Count == 0)
            {
                // we allow hooks to be registered to nonexistent commands, so the overall command list includes all keys of the hooks dictionary
                // if no more hooks exist, we need to remove that key so we don't expose "ghost" commands that do literally nothing
                Logger.LogDebug("No more hooks for command {Command}, performing cleanup", handler.Command);
                CommandHooks.Remove(handler.Command);
            }
        }
        else
        {
            // this should never happen since the only way to remove a hook is by disposing the returned IDisposable
            // so getting here indicates some sort of bad state
            Logger.LogError(
                "Attempted to remove command hook {Underlying} for command {Command} but no such hook exists",
                handler.UnderlyingFullName,
                handler.Command);

            throw new InvalidOperationException($"Cannot remove command hook {handler.UnderlyingFullName}: hook isn't registered");
        }
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
