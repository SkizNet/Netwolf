using Netwolf.PluginFramework.Context;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

public class ServerContext : ExtensibleContextBase
{
    public User? User { get; set; }

    public Channel? Channel { get; set; }

    public required object Server { get; set; }

    public override object Sender => Server;
}
