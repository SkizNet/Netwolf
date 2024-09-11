using Netwolf.PluginFramework.Context;
using Netwolf.PluginFramework.Permissions;
using Netwolf.Server.Exceptions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

public class ServerPermissionManager : IPermissionManager
{
    public Exception GetPermissionError(IContext context, string permission)
    {
        if (context is not ServerContext ctx || ctx.User == null)
        {
            throw new NotImplementedException();
        }

        // error depends on privilege scope
        string scope = permission[0..4];
        if (scope == "oper")
        {
            return ctx.User.HasPrivilege("oper:general") switch
            {
                true => new MissingOperPrivException(permission),
                false => new NoPrivException()
            };
        }
        else if (scope == "chan")
        {
            // ctx.Channel is guaranteed non-null by this point because CommandValidator already had a shot at it
            return new MissingChanPrivException(ctx.Channel!.Name);
        }

        // else scope is user
        return new NoPrivException();
    }

    public bool HasPermission(IContext context, string permission)
    {
        if (context is not ServerContext ctx || ctx.User == null)
        {
            throw new NotImplementedException();
        }

        return ctx.User.HasPrivilege(permission, ctx.Channel);
    }
}
