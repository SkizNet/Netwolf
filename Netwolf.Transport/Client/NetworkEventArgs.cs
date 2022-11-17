using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Arguments to events raised by <see cref="INetwork"/>.
    /// </summary>
    public class NetworkEventArgs : EventArgs
    {
        /// <summary>
        /// Network the event was raised for. Read-only.
        /// </summary>
        public INetwork Network { get; init; }

        /// <summary>
        /// Command being processed if event is related to a command.
        /// </summary>
        public ICommand? Command { get; init; }

        internal NetworkEventArgs(INetwork network, ICommand? command = null)
        {
            Network = network;
            Command = command;
        }
    }
}
