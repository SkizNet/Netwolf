using Netwolf.Server.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Capabilities;

public interface ICapability
{
    string Name { get; }

    string? Value => null;

    ICapability[] DependsOn => Array.Empty<ICapability>();

    ICapability[] ConflictsWith => Array.Empty<ICapability>();

    int GetHashCode()
    {
        return Name.GetHashCode();
    }

    bool Equals(object? obj)
    {
        if (obj is not ICapability cap)
        {
            return false;
        }

        return Name == cap.Name;
    }
}
