// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Options;

using Netwolf.BotFramework.Services;

namespace Netwolf.BotFramework.Permissions;

internal class SettingsFilePermissionProvider : IPermissionProvider
{
    private IOptionsMonitor<BotOptions> Options { get; init; }

    public SettingsFilePermissionProvider(IOptionsMonitor<BotOptions> options)
    {
        Options = options;
    }

    public Task<IEnumerable<string>> GetPermissionsAsync(BotCommandContext context, CancellationToken cancellationToken)
    {
        if (context.SenderAccount != null && Options.Get(context.Bot.BotName).Permissions.TryGetValue(context.SenderAccount, out var perms))
        {
            return Task.FromResult((IEnumerable<string>)perms);
        }

        return Task.FromResult((IEnumerable<string>)[]);
    }
}
