// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Events;

/// <summary>
/// Arguments for CAP-related events
/// </summary>
public sealed class CapEventArgs
{
    /// <summary>
    /// Network the event was raised for. Read-only.
    /// </summary>
    public INetwork Network { get; init; }

    /// <summary>
    /// Capability name. Read-only.
    /// </summary>
    public string CapName { get; init; }

    /// <summary>
    /// Capability value. Read-only.
    /// Will be null if the network did not specify a value for the CAP.
    /// </summary>
    public string? CapValue { get; init; }

    /// <summary>
    /// The CAP subcommand, will be one of LS or NEW for ShouldEnableCap, LIST or ACK for CapEnabled,
    /// and DEL for CapDisabled. No event is raised for NAK, so Subcommand will never be NAK.
    /// </summary>
    public string Subcommand { get; init; }

    /// <summary>
    /// Cancellation token for async operations.
    /// </summary>
    public CancellationToken Token { get; init; }

    /// <summary>
    /// Construct a new CapEventArgs
    /// </summary>
    /// <param name="network"></param>
    /// <param name="capName"></param>
    /// <param name="subcommand"></param>
    /// <param name="token"></param>
    public CapEventArgs(INetwork network, string capName, string? capValue, string subcommand, CancellationToken token)
    {
        Network = network;
        CapName = capName;
        CapValue = capValue;
        Subcommand = subcommand;
        Token = token;
    }
}
