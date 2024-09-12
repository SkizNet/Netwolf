using Netwolf.PluginFramework.Context;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework;

public class BotCommandContext : IContext
{
    public Bot Bot { get; init; }

    public string FullLine { get; init; }

    public BotCommandContext(Bot bot, string fullLine)
    {
        Bot = bot;
        FullLine = fullLine;
    }
}
