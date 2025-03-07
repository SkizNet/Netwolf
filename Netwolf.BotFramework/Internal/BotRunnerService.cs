﻿// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Netwolf.BotFramework.Services;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<Task> botTasks = [];

        foreach (var (botName, botType) in Registry.HostedTypes)
        {
            stoppingToken.ThrowIfCancellationRequested();

            // make a new DI scope for each bot so they can safely used scoped services for statekeeping
            var scope = ScopeFactory.CreateScope();
            Scopes[botName] = scope;

            // fire off the bot task but don't await it here since we want to start all bots
            var bot = (Bot)ActivatorUtilities.CreateInstance(
                scope.ServiceProvider,
                botType,
                scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(botName));

            Registry.RegisterBot(botName, bot);
            ManagedBots.Add(botName);

            // deferring to the thread pool (via Task.Run) ensures that the logger prints that the application has started
            // before any bot connections are made, which makes for prettier log output :)
            botTasks.Add(Task.Run(() => bot.ExecuteAsync(stoppingToken), CancellationToken.None));
        }

        // block until all bots are disconnected
        // eventually we could WaitAny() here to handle things like reconnect logic
        await Task.WhenAll(botTasks);
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
