using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    public string Name { get; init; }

    public string? Privilege { get; init; }

    public CommandAttribute(string name, string? privilege = null)
    {
        Name = name;
        Privilege = privilege;
    }
}
