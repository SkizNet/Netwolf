// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;

namespace Netwolf.Transport.Exceptions;

public class RateLimitLeaseAcquisitionException : Exception
{
    public ICommand Command { get; init; }

    public Dictionary<string, object?> LeaseMetadata { get; init; }

    public RateLimitLeaseAcquisitionException(ICommand command, Dictionary<string, object?> leaseMetadata)
        : base("Unable to acquire rate limiter lease")
    {
        Command = command;
        LeaseMetadata = leaseMetadata;
    }
}
