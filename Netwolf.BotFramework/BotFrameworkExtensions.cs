using Microsoft.Extensions.DependencyInjection;

using Netwolf.BotFramework.Internal;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Extensions.DependencyInjection;
using Netwolf.PluginFramework.Permissions;
using Netwolf.Transport.Extensions.DependencyInjection;

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
    /// All service keys for registered bots
    /// </summary>
    internal static Dictionary<string, Type> RegisteredBots = [];

    /// <summary>
    /// Registers a new bot to be executed in the background immediately.
    /// The process will exit once all bots finish running.
    /// </summary>
    /// <typeparam name="TBot"></typeparam>
    /// <param name="services"></param>
    /// <param name="botName">Internal name for the bot, to allow for multiple bots of the same <typeparamref name="TBot"/> to be registered.</param>
    /// <returns></returns>
    public static IServiceCollection AddBot<TBot>(this IServiceCollection services, string botName)
        where TBot : Bot
    {
        if (RegisteredBots.ContainsKey(botName))
        {
            throw new ArgumentException($"Bot names must be unique; received duplicate bot name {botName}", nameof(botName));
        }

        services.AddPluginFrameworkServices();
        // no-ops if transport services are already registered, so this is safe to call multiple times
        services.AddTransportServices();
        services.AddSingleton<BotRegistry>();
        services.AddHostedService<BotRunnerService>();
        services.AddSingleton<IPermissionManager, BotPermissionManager>();

        RegisteredBots[botName] = typeof(TBot);
        return services;
    }
}
