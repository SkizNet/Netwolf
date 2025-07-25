// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;

using System.Threading.RateLimiting;

namespace Netwolf.Transport.RateLimiting;

/// <summary>
/// Abstraction that allows for rate limiting of sent commands.
/// </summary>
public interface IRateLimiter
{
    ValueTask<RateLimitLease> AcquireAsync(ICommand command, CancellationToken cancellationToken = default);
}
