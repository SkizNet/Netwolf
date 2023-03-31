namespace Netwolf.Transport.Exceptions;

/// <summary>
/// Indicates a fatal error in an attempt to connect to a Network
/// </summary>
public class ConnectionException : Exception
{
    public ConnectionException() : base() { }
    public ConnectionException(string message) : base(message) { }
    public ConnectionException(string message, Exception innerException) : base(message, innerException) { }
}
