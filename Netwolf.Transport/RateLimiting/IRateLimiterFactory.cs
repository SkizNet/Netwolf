// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.Transport.RateLimiting;

public interface IRateLimiterFactory
{
    /// <summary>
    /// Create a new rate limiter instance
    /// </summary>
    /// <param name="options">Network options for the current connection</param>
    /// <returns>New rate limiter instance</returns>
    IRateLimiter Create(NetworkOptions options);
}
