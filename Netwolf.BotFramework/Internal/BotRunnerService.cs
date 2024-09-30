using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Netwolf.BotFramework.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Internal;

/// <summary>
/// Hosted background service that runs all registered bots.
/// </summary>
internal class BotRunnerService : BackgroundService
{
    private IServiceScopeFactory ScopeFactory { get; init; }

    private BotRegistry Registry { get; init; }

    private Dictionary<string, IServiceScope> Scopes { get; init; } = [];

    private List<string> ManagedBots { get; init; } = [];

    public BotRunnerService(IServiceScopeFactory scopeFactory, BotRegistry registry)
    {
        ScopeFactory = scopeFactory;
        Registry = registry;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<Task> botTasks = [];

        foreach (var (botName, botType) in BotFrameworkExtensions.RegisteredBots)
        {
            stoppingToken.ThrowIfCancellationRequested();

            // make a new DI scope for each bot so they can safely used scoped services for statekeeping
            var scope = ScopeFactory.CreateScope();
            Scopes[botName] = scope;

            // fire off the bot task but don't await it here since we want to start all bots
            var bot = (Bot)ActivatorUtilities.CreateInstance(
                scope.ServiceProvider,
                botType,
                botName,
                scope.ServiceProvider.GetKeyedServices<IAccountProvider>(botName),
                scope.ServiceProvider.GetKeyedServices<IPermissionProvider>(botName));
            Registry.RegisterBot(botName, bot);
            ManagedBots.Add(botName);
            botTasks.Add(bot.ExecuteAsync(stoppingToken));
        }

        // block until all bots are disconnected
        // eventually we could WaitAny() here to handle things like reconnect logic
        Task.WaitAll([.. botTasks], stoppingToken);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        base.Dispose();
        foreach (var botName in ManagedBots)
        {
            Registry.Remove(botName)?.Dispose();
        }

        foreach (var (_, scope) in Scopes)
        {
            scope.Dispose();
        }

        ManagedBots.Clear();
        Scopes.Clear();
    }
}
