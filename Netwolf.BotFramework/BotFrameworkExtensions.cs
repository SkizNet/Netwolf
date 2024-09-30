using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.BotFramework.Accounts;
using Netwolf.BotFramework.Internal;
using Netwolf.BotFramework.Permissions;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Extensions.DependencyInjection;
using Netwolf.PluginFramework.Permissions;
using Netwolf.Transport.Extensions.DependencyInjection;
using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    private static readonly BotRegistry _registry = new();

    private static bool _botServicesConfigured = false;

    /// <summary>
    /// Registers a new bot to be executed in the background immediately.
    /// The process will exit once all bots finish running.
    /// </summary>
    /// <typeparam name="TBot"></typeparam>
    /// <param name="services"></param>
    /// <param name="botName">Internal name for the bot, to allow for multiple bots of the same <typeparamref name="TBot"/> to be registered.</param>
    /// <returns>Service collection for fluent call chaining</returns>
    public static IServiceCollection AddBot<TBot>(this IServiceCollection services, string botName)
        where TBot : Bot
    {
        return AddBot<TBot>(services, botName, null);
    }

    /// <summary>
    /// Registers a new bot to be executed in the background immediately.
    /// The process will exit once all bots finish running.
    /// </summary>
    /// <typeparam name="TBot"></typeparam>
    /// <param name="services"></param>
    /// <param name="botName">Internal name for the bot, to allow for multiple bots of the same <typeparamref name="TBot"/> to be registered.</param>
    /// <param name="configuration">Configuration callback to customize additional aspects of the bot.</param>
    /// <returns>Service collection for fluent call chaining</returns>
    public static IServiceCollection AddBot<TBot>(this IServiceCollection services, string botName, Action<IBotBuilder>? configuration)
        where TBot : Bot
    {
        if (_registry.KnownTypes.ContainsKey(botName))
        {
            throw new ArgumentException($"Bot names must be unique; received duplicate bot name {botName}", nameof(botName));
        }

        if (!_botServicesConfigured)
        {
            // no-ops if plugin or transport services are already registered, so this is safe to call multiple times
            // (it may have been called already if other frameworks are in use alongside BotFramework)
            services.AddPluginFrameworkServices();
            services.AddTransportServices();

            // below is only called exactly once; TryAdd is not used for this reason
            services.AddSingleton<BotRegistry>(_registry);
            services.AddSingleton<IPermissionManager, BotPermissionManager>();
            services.AddHostedService<BotRunnerService>();
            _botServicesConfigured = true;
        }

        services.AddKeyedScoped<BotCommandContextFactory>(botName, static (provider, key) =>
        {
            return new BotCommandContextFactory(
                provider.GetKeyedServices<IAccountProvider>(key),
                provider.GetKeyedServices<IPermissionProvider>(key));
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
                provider.GetRequiredService<ICommandDispatcher<BotCommandResult>>(),
                provider.GetRequiredService<ICommandFactory>(),
                provider.GetRequiredKeyedService<BotCommandContextFactory>(key),
                provider.GetKeyedServices<ICapProvider>(key));
        });

        var builder = new BotBuilder(botName, services);
        configuration?.Invoke(builder);

        _registry.RegisterType(botName, typeof(TBot));
        return services;
    }

    /// <summary>
    /// Determine the recognized account of the user for the bot from an account specified by the ircd.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IBotBuilder UseServicesAccountStrategy(this IBotBuilder builder)
    {
        // TODO: support more than just account-tag (e.g. extended-join, account-notify, WHOX, etc. to get services account info)
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
    public static IBotBuilder UseOperTagPermissionStrategy(this IBotBuilder builder)
    {
        builder.Services
            .AddKeyedSingleton<IPermissionProvider, OperTagPermissionProvider>(builder.BotName)
            .AddKeyedSingleton<ICapProvider, OperTagPermissionProvider>(builder.BotName);

        return builder;
    }
}
