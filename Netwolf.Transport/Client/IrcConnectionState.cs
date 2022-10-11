using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Holds the state of the client's connection
    /// </summary>
    public class IrcConnectionState
    {
        /// <summary>
        /// The current nickname for the connection
        /// </summary>
        public string Nick { get; internal set; } = default!;

        /// <summary>
        /// Our ident for this connection
        /// </summary>
        public string Ident { get; internal set; } = default!;

        /// <summary>
        /// Our host / vhost for this connection
        /// </summary>
        public string Host { get; internal set; } = default!;

        /// <summary>
        /// Our account for this connection, or <c>null</c> if we don't have one
        /// </summary>
        public string? Account { get; internal set; }
    }
}
