// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.BotFramework.Services;

/// <summary>
/// Service that, when given an account, retrieves all permissions associated with that account.
/// If multiple permission providers are registered for a bot, all permissions from all providers
/// will be merged together.
/// </summary>
public interface IPermissionProvider
{
    Task<IEnumerable<string>> GetPermissionsAsync(BotCommandContext context, CancellationToken cancellationToken);
}
