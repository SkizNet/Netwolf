using Microsoft.Extensions.DependencyInjection;

using Netwolf.BotFramework.Internal;
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
    internal static HashSet<string> RegisteredBots = [];

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
        if (RegisteredBots.Contains(botName))
        {
            throw new ArgumentException($"Bot names must be unique; received duplicate bot name {botName}", nameof(botName));
        }

        // no-ops if transport services are already registered, so this is safe to call multiple times
        services.AddTransportServices();
        services.AddHostedService<BotRunnerService>();

        services.AddKeyedTransient<IBot, TBot>(botName, (provider, key) => ActivatorUtilities.CreateInstance<TBot>(provider, [key!]));
        RegisteredBots.Add(botName);
        return services;
    }
}
