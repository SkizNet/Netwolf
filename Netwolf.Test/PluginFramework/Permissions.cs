using Netwolf.PluginFramework.Permissions;
using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.PluginFramework;
public class Permissions
{
    public class AllowAll : IPermissionManager
    {
        public Exception GetPermissionError(IContext context, string permission)
        {
            // should never be called so throw an exception here
            throw new InvalidOperationException();
        }

        public bool HasPermission(IContext context, string permission) => true;
    }
}
