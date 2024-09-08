using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Attributes;

public enum CommandTargeting
{
    /// <summary>
    /// Command can be executed in both channel and PM contexts
    /// </summary>
    AllowAll,
    /// <summary>
    /// Command may only be executed in channel contexts
    /// </summary>
    ChannelOnly,
    /// <summary>
    /// Command may only be executed in private contexts
    /// </summary>
    PrivateOnly
}
