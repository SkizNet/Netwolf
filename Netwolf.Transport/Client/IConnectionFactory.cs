using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Factory for <see cref="IConnection"/>, registered as a DI service
    /// </summary>
    public interface IConnectionFactory
    {
        /// <summary>
        /// Create a new connection.
        /// </summary>
        /// <param name="network">Network this connection is for.</param>
        /// <param name="server">Server on the network to connect to.</param>
        /// <param name="options">Network options.</param>
        /// <returns>A disconnected IConnection instance</returns>
        IConnection Create(INetwork network, IServer server, NetworkOptions options);
    }
}
