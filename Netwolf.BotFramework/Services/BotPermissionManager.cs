// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Netwolf.BotFramework.Exceptions;
using Netwolf.PluginFramework.Context;
using Netwolf.PluginFramework.Permissions;

namespace Netwolf.BotFramework.Services;

public class BotPermissionManager : IPermissionManager
{
    public Exception GetPermissionError(IContext context, string permission)
    {
        if (context is not BotCommandContext botContext)
        {
            throw new ArgumentException("BotPermissionManager.GetPermissionError called with a context that is not BotCommandContext", nameof(context));
        }

        return new PermissionException(botContext.SenderNickname, botContext.SenderAccount, botContext.Command.Verb, permission);
    }

    public bool HasPermission(IContext context, string permission)
    {
        if (context is not BotCommandContext botContext)
        {
            throw new NotImplementedException();
        }

        return botContext.SenderPermissions.Contains(permission);
    }
}
