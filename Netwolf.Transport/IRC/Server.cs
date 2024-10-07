// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

/// <summary>
/// A server we can connect to as a client.
/// </summary>
public class Server : IServer
{
    /// <summary>
    /// Server hostname (DNS name or IP) to connect to.
    /// </summary>
    /// <remarks>
    /// <seealso cref="Port"/>
    /// <seealso cref="SecureConnection"/>
    /// </remarks>
    public string HostName { get; set; }

    /// <summary>
    /// Port number to connect to.
    /// </summary>
    /// <remarks>
    /// <seealso cref="Address"/>
    /// <seealso cref="SecureConnection"/>
    /// </remarks>
    public int Port { get; set; }

    private readonly int[] SECURE_PORTS = { 6697, 9999 };
    private bool? _secureConnection;

    /// <summary>
    /// Whether or not to connect to this server using TLS.
    /// By default, TLS is used if the <see cref="Port"/> is <c>6697</c> or <c>9999</c>.
    /// </summary>
    public bool SecureConnection
    {
        get => _secureConnection ?? SECURE_PORTS.Contains(Port);
        set => _secureConnection = value;
    }

    public Server(string hostName, int port, bool? secure = null)
    {
        ArgumentNullException.ThrowIfNull(hostName);
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port number must be between 1 and 65535.");
        }

        HostName = hostName;
        Port = port;
        if (secure != null)
        {
            SecureConnection = secure.Value;
        }
    }

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
