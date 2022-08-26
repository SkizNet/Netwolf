using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Type (direction) of an <see cref="ICommand"/>
    /// </summary>
    public enum CommandType
    {
        /// <summary>
        /// Command sent from the client to the server
        /// </summary>
        Client,
        /// <summary>
        /// Command sent from the server to the client
        /// </summary>
        Server
    }
}
