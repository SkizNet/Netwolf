// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;
using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Events;

/// <summary>
/// Arguments for SASL events
/// </summary>
public sealed class SaslEventArgs
{
    /// <summary>
    /// Network the event was raised for. Read-only.
    /// </summary>
    public INetwork Network { get; init; }

    /// <summary>
    /// The SASL mechanism being used; null if no mechanism. Read-only.
    /// </summary>
    public string? Mechanism { get; init; }

    /// <summary>
    /// Cancellation token to use for any asynchronous tasks awaited by the event.
    /// </summary>
    public CancellationToken Token { get; init; }

    /// <summary>
    /// Construct a new SaslEventArgs
    /// </summary>
    /// <param name="network"></param>
    /// <param name="mechanism"></param>
    /// <param name="token"></param>
    public SaslEventArgs(INetwork network, string? mechanism, CancellationToken token)
    {
        Network = network;
        Mechanism = mechanism;
        Token = token;
    }
}
