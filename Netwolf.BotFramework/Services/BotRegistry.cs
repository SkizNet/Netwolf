// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.BotFramework.Services;

/// <summary>
/// Service that tracks <see cref="Bot"/> instances that have been registered via <see cref="BotFrameworkExtensions.AddBot{TBot}(Microsoft.Extensions.DependencyInjection.IServiceCollection, string)"/>. />
/// </summary>
public class BotRegistry
{
    private Dictionary<string, Bot> KnownBots { get; init; } = [];

    private readonly Dictionary<string, Type> _types = [];

    internal IReadOnlyDictionary<string, Type> KnownTypes => _types;

    internal List<(string Name, Type Type)> HostedTypes { get; init; } = [];

    internal BotRegistry() { }

    internal void RegisterType(string name, Type type, bool runImmediately)
    {
        _types.Add(name, type);

        if (runImmediately)
        {
            HostedTypes.Add((name, type));
        }
    }

    internal void RegisterBot(string name, Bot bot)
    {
        KnownBots.Add(name, bot);
    }

    internal Bot? Remove(string name)
    {
        KnownBots.Remove(name, out var bot);
        return bot;
    }

    public Bot GetBotByName(string name)
    {
        return KnownBots[name];
    }
}
