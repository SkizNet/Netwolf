// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.BotFramework.RateLimiting;

public class SlidingWindowConfig
{
    /// <summary>
    /// Whether this rate limiter is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The duration (in milliseconds) a window lasts.
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// The maximum number of messages per window that will be allowed per sliding window duration.
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// How many segments to subdivide <see cref="Duration"/> into to implement the sliding logic.
    /// If 1, acts the same as a fixed window rate limiter.
    /// </summary>
    public int Segments { get; set; } = 5;
}
