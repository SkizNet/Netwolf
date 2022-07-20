namespace Netwolf.Transport
{
    /// <summary>
    /// Represents a registered Account on a network
    /// </summary>
    public interface IAccount
    {
        /// <summary>
        /// Account name
        /// </summary>
        public string Name { get; }
    }
}
