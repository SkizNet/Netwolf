// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.BotFramework.RateLimiting;

public class TokenBucketConfig
{
    /// <summary>
    /// Whether this rate limiter is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The maximum number of tokens that can be accrued.
    /// The token bucket begins with this many tokens as well.
    /// </summary>
    public int MaxTokens { get; set; }

    /// <summary>
    /// The time (in milliseconds) it takes to regenerate <see cref="ReplenismentAmount"/> tokens,
    /// up to a maximum of <see cref="MaxTokens"/> tokens.
    /// </summary>
    public int ReplenishmentRate { get; set; }

    /// <summary>
    /// The number of tokens to regenerate per <see cref="ReplenishmentRate"/>.
    /// </summary>
    public int ReplenismentAmount { get; set; } = 1;
}
