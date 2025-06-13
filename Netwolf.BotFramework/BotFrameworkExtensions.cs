// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.BotFramework.Accounts;
using Netwolf.BotFramework.Internal;
using Netwolf.BotFramework.Permissions;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Extensions.DependencyInjection;
using Netwolf.PluginFramework.Permissions;
using Netwolf.Transport.Events;
using Netwolf.Transport.Extensions.DependencyInjection;
using Netwolf.Transport.IRC;

namespace Netwolf.BotFramework;

/// <summary>
/// Extension methods for DI registration of bots
/// </summary>
public static class BotFrameworkExtensions
{
    /// <summary>
    /// Singleton service instance for BotRegistry; created here instead of via DI activation
    /// so that we can refer to it before the service provider is fully built
    /// </summary>
    private static readonly Dictionary<IServiceCollection, BotRegistry> _registry = [];

    /// <summary>
    /// Registers a new bot to be executed in the background immediately.
    /// The process will exit once all bots finish running.
    /// </summary>
    /// <typeparam name="TBot"></typeparam>
    /// <param name="services"></param>
    /// <param name="botName">Internal name for the bot, to allow for multiple bots of the same <typeparamref name="TBot"/> to be registered.</param>
    /// <returns>Service collection for fluent call chaining</returns>
    public static IBotBuilder AddHostedBot<TBot>(this IServiceCollection services, string botName)
        where TBot : Bot
    {
        return AddBot<TBot>(services, botName, true);
    }

    /// <summary>
    /// Registers a new bot. It will not be run automatically; it is up to the caller to manage the bot's lifecycle.
    /// </summary>
    /// <typeparam name="TBot"></typeparam>
    /// <param name="services"></param>
    /// <param name="botName">Internal name for the bot, to allow for multiple bots of the same <typeparamref name="TBot"/> to be registered.</param>
    /// <returns>Service collection for fluent call chaining</returns>
    public static IBotBuilder AddBot<TBot>(this IServiceCollection services, string botName)
        where TBot : Bot
    {
        return AddBot<TBot>(services, botName, false);
    }

    /// <summary>
    /// Registers a new bot.
    /// </summary>
    /// <typeparam name="TBot"></typeparam>
    /// <param name="services"></param>
    /// <param name="botName">Internal name for the bot, to allow for multiple bots of the same <typeparamref name="TBot"/> to be registered.</param>
    /// <param name="runImmediately">Whether the bot should run immediately (be hosted in BotRunnerService).</param>
    /// <returns>Service collection for fluent call chaining</returns>
    private static IBotBuilder AddBot<TBot>(this IServiceCollection services, string botName, bool runImmediately)
        where TBot : Bot
    {
        if (!_registry.ContainsKey(services))
        {
            _registry[services] = new BotRegistry();
        }

        if (_registry[services].KnownTypes.ContainsKey(botName))
        {
            throw new ArgumentException($"Bot names must be unique; received duplicate bot name {botName}", nameof(botName));
        }

        if (!services.Any(s => s.ServiceType == typeof(BotRegistry)))
        {
            // no-ops if plugin or transport services are already registered, so this is safe to call multiple times
            // (it may have been called already if other frameworks are in use alongside BotFramework)
            services.AddPluginFrameworkServices();
            services.AddTransportServices();

            // below is only called exactly once; TryAdd is not used for this reason
            services.AddSingleton<BotRegistry>(_registry[services]);
            services.AddSingleton<IPermissionManager, BotPermissionManager>();
        }

        if (runImmediately && !services.Any(s => s.ImplementationType == typeof(BotRunnerService)))
        {
            services.AddHostedService<BotRunnerService>();
        }

        services.AddKeyedScoped<BotCommandContextFactory>(botName, static (provider, key) =>
        {
            return new BotCommandContextFactory(
                provider.GetKeyedServices<IAccountProvider>(key),
                provider.GetKeyedServices<IPermissionProvider>(key),
                provider.GetRequiredService<IValidationContextFactory>());
        });

        // BotRunnerService passes this explicitly via an extra parameter in ActivatorUtilities.CreateInstance,
        // however we register it as a DI service here so that bots created outside of BotRunnerService can still
        // receive the appropriate creation data injected into their constructors via FromKeyedServiceAttribute
        services.AddKeyedScoped<BotCreationData>(botName, static (provider, key) =>
        {
            return new BotCreationData(
                (string)key!,
                provider.GetRequiredService<ILogger<Bot>>(),
                provider.GetRequiredService<IOptionsMonitor<BotOptions>>(),
                provider.GetRequiredService<INetworkFactory>(),
                provider.GetRequiredService<NetworkEvents>(),
                provider.GetRequiredService<ICommandDispatcher<BotCommandResult>>(),
                provider.GetRequiredService<ICommandFactory>(),
                provider.GetRequiredKeyedService<BotCommandContextFactory>(key),
                provider.GetKeyedServices<ICapProvider>(key));
        });

        services.AddOptions<BotOptions>(botName)
            .BindConfiguration($"Netwolf.Bot:{botName}")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        _registry[services].RegisterType(botName, typeof(TBot), runImmediately);
        return new BotBuilder(botName, services);
    }

    /// <summary>
    /// Determine the recognized account of the user for the bot from an account specified by the ircd.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IBotBuilder AddServicesAccountStrategy(this IBotBuilder builder)
    {
        builder.Services
            .AddKeyedSingleton<IAccountProvider, ServicesAccountProvider>(builder.BotName)
            .AddKeyedSingleton<ICapProvider, ServicesAccountProvider>(builder.BotName);

        return builder;
    }

    /// <summary>
    /// Define an "oper" permission based on the presence of the oper message tag.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IBotBuilder AddOperTagPermissionStrategy(this IBotBuilder builder)
    {
        builder.Services
            .AddKeyedSingleton<IPermissionProvider, OperTagPermissionProvider>(builder.BotName)
            .AddKeyedSingleton<ICapProvider, OperTagPermissionProvider>(builder.BotName);

        return builder;
    }

    /// <summary>
    /// Retrieve permissions from appsettings.json. Permissions must be an object named "Permissions" with keys
    /// being the account name (based on any employed account strategies) and the value being an array
    /// of string permission names.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IBotBuilder AddSettingsFilePermissionStrategy(this IBotBuilder builder)
    {
        builder.Services.AddKeyedSingleton<IPermissionProvider, SettingsFilePermissionProvider>(builder.BotName);

        return builder;
    }

    private static readonly HashSet<string> MESSAGE_TYPES = ["PRIVMSG", "NOTICE", "TAGMSG", "CPRIVMSG", "CNOTICE"];

    /// <summary>
    /// Retrieves the target of a message (PRIVMSG, NOTICE, or TAGMSG).
    /// </summary>
    /// <param name="command">Command to retrieve the target from</param>
    /// <returns>The message target, or <c>null</c> if <paramref name="command"/> is not a PRIVMSG, NOTICE, or TAGMSG command.</returns>
    public static string? GetMessageTarget(this ICommand command)
    {
        if (MESSAGE_TYPES.Contains(command.Verb))
        {
            return command.Args[0];
        }

        return null;
    }
}
