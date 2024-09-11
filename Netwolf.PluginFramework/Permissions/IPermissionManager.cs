using Netwolf.PluginFramework.Context;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.PluginFramework.Permissions;

/// <summary>
/// Service used to designate whether a particular <see cref="IContext"/> has permission to
/// execute various actions. There is no default service implementation; individual frameworks
/// provide their own implementations.
/// </summary>
public interface IPermissionManager
{
    /// <summary>
    /// Check if the given context has the specified permission.
    /// A <see cref="NotImplementedException"/> should be thrown if the given
    /// <paramref name="context"/> is not a supported type by this particular permission manager.
    /// This allows multiple service implementations to be registered in the event multiple frameworks are in use.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    bool HasPermission(IContext context, string permission);

    /// <summary>
    /// Retrieve an appropriate permission error for the given context and permission. The exception
    /// types utilized vary by framework.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    Exception GetPermissionError(IContext context, string permission);
}
