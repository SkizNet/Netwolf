using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Exceptions;

internal class PermissionException : Exception
{
    public string Nick { get; init; }
    public string Account { get; init; }
    public string Command { get; init; }
    public string Permission { get; init; }

    public PermissionException(string nick, string? account, string command, string permission)
        : base($"The user {nick} (account {account ?? "<none>"}) does not have permission to execute {command} (missing {permission})")
    {
        Nick = nick;
        Account = account ?? "<none>";
        Command = command;
        Permission = permission;
    }
}
