// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Events;

/// <summary>
/// Singleton service that provides events when a network is connected
/// or disconnected. This allows for other global/singleton services to
/// be network-aware without needing to deal with individual network scopes.
/// </summary>
public sealed class NetworkEvents
{
    /// <summary>
    /// Event raised whenever we first establish a connection to a network.
    /// This happens after the socket is established but before user registration occurs.
    /// The <c>sender</c> parameter is the network we connected to, however
    /// make use of the <see cref="NetworkEventArgs"/> to obtain this in a
    /// strongly-typed manner instead.
    /// </summary>
    public event EventHandler<NetworkEventArgs>? NetworkConnecting;

    /// <summary>
    /// Event raised whenever we become fully connected to a network.
    /// This happens after connection registration completes.
    /// The <c>sender</c> parameter is the network we connected to, however
    /// make use of the <see cref="NetworkEventArgs"/> to obtain this in a
    /// strongly-typed manner instead.
    /// </summary>
    public event EventHandler<NetworkEventArgs>? NetworkConnected;

    /// <summary>
    /// Event raised whenever we become disconnected to a network for any reason.
    /// The <c>sender</c> parameter will be the network we disconnected from,
    /// however make use of the <see cref="NetworkEventArgs"/> to obtain this
    /// in a strongly-typed manner instead.
    /// </summary>
    public event EventHandler<NetworkEventArgs>? NetworkDisconnected;

    /// <summary>
    /// Fire the <see cref="NetworkConnecting"/> event for the specified network.
    /// This should only be called from an <see cref="INetwork"/> implementation.
    /// </summary>
    /// <param name="network">Network we are connecting to.</param>
    public void OnConnecting(INetwork network)
    {
        NetworkConnecting?.Invoke(network, new(network));
    }

    /// <summary>
    /// Fire the <see cref="NetworkConnected"/> event for the specified network.
    /// This should only be called from an <see cref="INetwork"/> implementation.
    /// </summary>
    /// <param name="network">Network we connected to.</param>
    public void OnConnected(INetwork network)
    {
        NetworkConnected?.Invoke(network, new(network));
    }

    /// <summary>
    /// Fire the <see cref="NetworkDisconnected"/> event for the specified network.
    /// This should only be called from an <see cref="INetwork"/> implementation.
    /// </summary>
    /// <param name="network">Network we disconnected from.</param>
    /// <param name="ex">Exception that caused the disconnect, if any.</param>
    public void OnDisconnected(INetwork network, Exception? ex = null)
    {
        NetworkDisconnected?.Invoke(network, new(network, ex));
    }
}
