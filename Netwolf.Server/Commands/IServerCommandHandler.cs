using Netwolf.PluginFramework.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public interface IServerCommandHandler : ICommandHandler<ICommandResponse>
{
    public bool AllowBeforeRegistration => false;

    public bool HasChannel => false;
}
