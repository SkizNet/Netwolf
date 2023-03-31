namespace Netwolf.Transport.Client;

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
