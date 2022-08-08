namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Represents a Server on a Network.
    /// </summary>
    public interface IServer
    {
        /// <summary>
        /// Server hostname
        /// </summary>
        public string HostName { get; }
    }
}
