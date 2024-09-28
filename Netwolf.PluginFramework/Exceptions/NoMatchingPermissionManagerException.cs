using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.PluginFramework.Exceptions;

public class NoMatchingPermissionManagerException : InvalidOperationException
{
    public string CommandName { get; init; }
    public Type ContextType { get; init; }
    public string Permission { get; init; }

    public NoMatchingPermissionManagerException(string commandName, Type contextType, string permission)
        : base("No registered permission managers support the given context and permission")
    {
        CommandName = commandName;
        ContextType = contextType;
        Permission = permission;
    }
}
