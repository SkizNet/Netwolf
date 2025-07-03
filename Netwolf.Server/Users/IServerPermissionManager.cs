using Netwolf.PluginFramework.Permissions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

public interface IServerPermissionManager : IPermissionManager
{
    IEnumerable<string> GetUserPermissionsForRole(string provider, string role);
}
