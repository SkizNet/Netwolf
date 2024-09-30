using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Internal;

internal sealed class BotBuilder : IBotBuilder
{
    public string BotName { get; init; }

    public IServiceCollection Services { get; init; }

    public BotBuilder(string botName, IServiceCollection services)
    {
        BotName = botName;
        Services = services;
    }
}
