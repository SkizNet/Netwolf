using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework;

/// <summary>
/// Configuration builder for a Bot
/// </summary>
public interface IBotBuilder
{
    /// <summary>
    /// Bot name; also used as service key
    /// </summary>
    string BotName { get; }

    /// <summary>
    /// The service collection in use
    /// </summary>
    IServiceCollection Services { get; }
}
