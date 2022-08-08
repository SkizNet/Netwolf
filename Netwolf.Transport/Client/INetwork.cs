namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Represents a Network
    /// </summary>
    public interface INetwork : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Network name
        /// </summary>
        public string Name { get; }
    }
}
