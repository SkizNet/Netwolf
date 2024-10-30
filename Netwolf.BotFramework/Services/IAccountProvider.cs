// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.BotFramework.Services;

/// <summary>
/// Service to resolve a sender into an account recognized by the bot.
/// If multiple account providers are registered to a single bot, they are tried in order;
/// the first one that returns a valid account will be used.
/// </summary>
public interface IAccountProvider
{
    Task<string?> GetAccountAsync(BotCommandContext context, CancellationToken cancellationToken);
}
