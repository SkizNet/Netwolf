// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Events;

/// <summary>
/// Arguments to events raised by <see cref="INetwork"/>.
/// </summary>
public class NetworkEventArgs : EventArgs
{
    /// <summary>
    /// Network the event was raised for. Read-only.
    /// </summary>
    public INetwork Network { get; init; }

    /// <summary>
    /// Exception raised by the event, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    internal NetworkEventArgs(INetwork network, Exception? ex = null)
    {
        Network = network;
        Exception = ex;
    }
}
