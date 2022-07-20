namespace Netwolf.Transport
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

        /// <summary>
        /// Network this server belongs to
        /// </summary>
        public INetwork Network { get; }
    }
}
