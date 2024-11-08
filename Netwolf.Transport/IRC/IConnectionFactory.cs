// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.State;

namespace Netwolf.Transport.IRC;

/// <summary>
/// Factory for <see cref="IConnection"/>, registered as a DI service
/// </summary>
public interface IConnectionFactory
{
    /// <summary>
    /// Create a new connection.
    /// </summary>
    /// <param name="network">Network this connection is for.</param>
    /// <param name="server">Server on the network to connect to.</param>
    /// <param name="options">Network options.</param>
    /// <returns>A disconnected IConnection instance</returns>
    IConnection Create(INetwork network, ServerRecord server, NetworkOptions options);
}
