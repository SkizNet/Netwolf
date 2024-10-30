using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Internal;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal class ServerCommandAttribute : Attribute
{
    internal string Command { get; init; }

    internal ServerCommandAttribute(string command)
    {
        Command = command;
    }
}
