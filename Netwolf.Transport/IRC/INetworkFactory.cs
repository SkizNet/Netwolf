namespace Netwolf.Transport.IRC;

/// <summary>
/// Factory for <see cref="INetwork"/>, registered as a DI service
/// </summary>
public interface INetworkFactory
{
    /// <summary>
    /// Create a new network with the given options.
    /// </summary>
    /// <param name="name">
    /// Network name, for the caller's internal tracking purposes.
    /// The name does not need to be unique.
    /// </param>
    /// <param name="options">Network options.</param>
    /// <returns>A disconnected network instance</returns>
    INetwork Create(string name, NetworkOptions options);
}
