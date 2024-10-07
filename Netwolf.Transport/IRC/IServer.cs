// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

/// <summary>
/// Represents a Server on a Network.
/// </summary>
public interface IServer
{
    /// <summary>
    /// Server hostname
    /// </summary>
    public string HostName { get; }

    /// <summary>
    /// Server port
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Whether or not this connection is encrypted
    /// </summary>
    public bool SecureConnection { get; }
}
