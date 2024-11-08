// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;

namespace Netwolf.Transport.State;

/// <summary>
/// A server we can connect to as a client.
/// </summary>
public sealed record ServerRecord(
    string HostName,
    [Range(1, 65535)] int Port,
    bool? UseTls = null,
    string? Password = null)
{
    private static readonly int[] SECURE_PORTS = [6697, 9999];

    /// <summary>
    /// Whether or not to connect to this server using TLS.
    /// By default, TLS is used if the <see cref="Port"/> is <c>6697</c> or <c>9999</c>.
    /// </summary>
    public bool SecureConnection => UseTls ?? SECURE_PORTS.Contains(Port);

    /// <summary>
    /// Convert the server to a string representation of the host name, port, and TLS setting.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        string sslChar = SecureConnection ? "+" : String.Empty;
        string leading = String.Empty;
        string trailing = String.Empty;

        if (HostName.Contains(':'))
        {
            leading = "[";
            trailing = "]";
        }

        return $"{leading}{HostName}{trailing}:{sslChar}{Port}";
    }
}
