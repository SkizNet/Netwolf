// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.Transport.RateLimiting;

internal class RateLimiterFactory : IRateLimiterFactory
{
    /// <inheritdoc />
    public IRateLimiter Create(NetworkOptions options)
    {
        return new RateLimiter(options);
    }
}
