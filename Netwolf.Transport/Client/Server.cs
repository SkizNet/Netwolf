
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// A server we can connect to as a client.
    /// </summary>
    public class Server : IServer
    {
        /// <summary>
        /// Server hostname (DNS name or IP) to connect to.
        /// </summary>
        /// <remarks>
        /// <seealso cref="Port"/>
        /// <seealso cref="SecureConnection"/>
        /// </remarks>
        public string HostName { get; set; }

        /// <summary>
        /// Port number to connect to.
        /// </summary>
        /// <remarks>
        /// <seealso cref="Address"/>
        /// <seealso cref="SecureConnection"/>
        /// </remarks>
        public int Port { get; set; }

        private readonly int[] SECURE_PORTS = { 6697, 9999 };
        private bool? _secureConnection;

        /// <summary>
        /// Whether or not to connect to this server using TLS.
        /// By default, TLS is used if the <see cref="Port"/> is <c>6697</c> or <c>9999</c>.
        /// </summary>
        public bool SecureConnection
        {
            get => _secureConnection ?? SECURE_PORTS.Contains(Port);
            set => _secureConnection = value;
        }

        internal Server(string hostName, int port, bool? secure)
        {
            ArgumentNullException.ThrowIfNull(hostName);
            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), port, "Port number must be between 1 and 65535.");
            }

            HostName = hostName;
            Port = port;
            if (secure != null)
            {
                SecureConnection = secure.Value;
            }
        }
    }
}
