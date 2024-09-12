using Netwolf.PluginFramework.Context;
using Netwolf.PluginFramework.Permissions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Services;

public class BotPermissionManager : IPermissionManager
{
    public Exception GetPermissionError(IContext context, string permission)
    {
        throw new NotImplementedException();
    }

    public bool HasPermission(IContext context, string permission)
    {
        throw new NotImplementedException();
    }
}
