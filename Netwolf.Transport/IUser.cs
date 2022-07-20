namespace Netwolf.Transport
{
    /// <summary>
    /// Represents a User connected to a Server
    /// </summary>
    public interface IUser
    {
        /// <summary>
        /// The user's nickname
        /// </summary>
        public string Nick { get; }

        /// <summary>
        /// The user's ident
        /// </summary>
        public string Ident { get; }

        /// <summary>
        /// The user's host
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// The user's IP address, if known
        /// </summary>
        public string? IP { get; }

        /// <summary>
        /// The user's realname (gecos)
        /// </summary>
        public string RealName { get; }

        /// <summary>
        /// The user's account, if any and if known
        /// </summary>
        public IAccount? Account { get; }

        /// <summary>
        /// Server this user is known to exist on;
        /// when operating in client mode this will always be the Server we are directly connected to
        /// </summary>
        public IServer Server { get; }
    }
}