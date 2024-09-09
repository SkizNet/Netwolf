using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Services;

/// <summary>
/// Service that tracks <see cref="Bot"/> instances that have been registered via <see cref="BotFrameworkExtensions.AddBot{TBot}(Microsoft.Extensions.DependencyInjection.IServiceCollection, string)"/>. />
/// </summary>
public class BotRegistry
{
    private Dictionary<string, Bot> KnownBots { get; init; } = [];

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
