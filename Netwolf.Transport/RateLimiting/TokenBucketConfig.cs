// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.RateLimiting;

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
    /// The time (in milliseconds) it takes to regenerate <see cref="ReplenishmentAmount"/> tokens,
    /// up to a maximum of <see cref="MaxTokens"/> tokens.
    /// </summary>
    public int ReplenishmentRate { get; set; }

    /// <summary>
    /// The number of tokens to regenerate per <see cref="ReplenishmentRate"/>.
    /// </summary>
    public int ReplenishmentAmount { get; set; } = 1;
}
