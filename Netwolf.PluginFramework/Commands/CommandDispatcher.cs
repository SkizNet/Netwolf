using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.PluginFramework.Context;
using Netwolf.PluginFramework.Permissions;

using System.Reflection;
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

        // Add builtin commands
        // FIXME: once we define the shape of the plugin framework a bit more, probably move this to assembly-level attributes to define plugins? dunno
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            AddCommandsFromAssembly(assembly);
        }
    }

    public void AddCommandsFromAssembly(Assembly assembly)
    {
        // populate Commands from all concrete classes across all assemblies in the current AssemblyLoadContext that implement ICommandHandler
        Logger.LogTrace("Scanning for commands in {Name}", assembly.FullName);
        foreach (var type in assembly.ExportedTypes)
        {
            if (!type.IsAssignableTo(typeof(ICommandHandler<TResult>)) || type.IsAbstract || type.IsInterface)
            {
                Logger.LogTrace(@"Skipping {Type}: is abstract, interface, or not assignable to command handler type", type.FullName);
                continue;
            }

            if (type.IsGenericType)
            {
                // warn on non-abstract generics since this indicates a likely programmer mistake
                Logger.LogWarning(@"Skipping {Type}: generic types are not currently supported", type.FullName);
                continue;
            }

            if (!Validator.ValidateCommandType(type))
            {
                Logger.LogDebug(@"Skipping {Type}: type validator returned false", type.FullName);
                continue;
            }

            // now that we've gotten this far, instantiate it
            ICommandHandler<TResult> handler = (ICommandHandler<TResult>)ActivatorUtilities.CreateInstance(ServiceProvider, type);

            if (!CommandNameRegex().IsMatch(handler.Command))
            {
                Logger.LogWarning(@"Skipping {Type}: bad command name {Command}", type.FullName, handler.Command);
                continue;
            }

            if (!Validator.ValidateCommandHandler(handler))
            {
                Logger.LogDebug(@"Skipping {Type}: handler validator returned false", type.FullName);
                continue;
            }

            Commands.Add(handler.Command, handler);
            Logger.LogInformation("Found {Type} providing {Command}", type.FullName, handler.Command);
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

                throw new InvalidOperationException("No registered permission managers support the given context and permission");
            }

            if (!hasPermission)
            {
                throw selectedManager.GetPermissionError(sender, handler.Privilege);
            }
        }

        return handler.ExecuteAsync(command, sender, cancellationToken)!;
    }
}
