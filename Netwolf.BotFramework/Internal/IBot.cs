using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Internal;

/// <summary>
/// DI interface that registers a bot with the <see cref="BotRunnerService"/>.
/// </summary>
internal interface IBot
{
    /// <summary>
    /// Connects the bot to the network and handle events until it is disconnected
    /// or a stop is requested.
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    Task ExecuteAsync(CancellationToken stoppingToken);
}
