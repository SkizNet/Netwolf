using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// A network that we connect to as a client
    /// </summary>
    public class Network : INetwork
    {
        public string Name { get; set; }

        /// <summary>
        /// Primary nickname we attempt to use when connecting to this network
        /// </summary>
        /// <remarks>
        /// <seealso cref="SecondaryNick"/>
        /// </remarks>
        public string PrimaryNick { get; set; }

        /// <summary>
        /// Secondary nickname we attempt to use when connecting to this network,
        /// in the event <see cref="PrimaryNick"/> is in use.
        /// </summary>
        public string? SecondaryNick { get; set; }

        private string? _ident;

        /// <summary>
        /// Ident to use. Defaults to <see cref="PrimaryNick"/> if not explicitly set.
        /// </summary>
        public string Ident
        {
            get => _ident ?? PrimaryNick;
            set => _ident = value;
        }

        /// <summary>
        /// Password required to connect to the network, if any.
        /// </summary>
        public string? ServerPassword { get; set; }

        /// <summary>
        /// Local host to bind to when connecting. If unset, use default outbound IP.
        /// </summary>
        public string? BindHost { get; set; }

        /// <summary>
        /// List of servers to try when connecting (in order of preference).
        /// </summary>
        public List<Server> ServerList { get; init; } = new List<Server>();

        private string? _accountName;

        /// <summary>
        /// Account name to use. Defaults to <see cref="PrimaryNick"/> if not explicitly set.
        /// </summary>
        public string AccountName
        {
            get => _accountName ?? PrimaryNick;
            set => _accountName = value;
        }

        /// <summary>
        /// Password to log into the user's account.
        /// </summary>
        public string? AccountPassword { get; set; }

        /// <summary>
        /// TLS client certificate used to log into the user's account.
        /// </summary>
        public X509Certificate? AccountCertificate { get; set; }

        /// <summary>
        /// Authentication type to use. If unset, uses the most secure method available
        /// to us based on the network's SASL support and whether <see cref="AccountCertificate"/> or
        /// <see cref="AccountPassword"/> are defined. The following are tried (in order):
        /// <list type="number">
        /// <item><description>SASL EXTERNAL</description></item>
        /// <item><description>SASL SCRAM-SHA256</description></item>
        /// <item><description>SASL PLAIN</description></item>
        /// <item><description>NickServ IDENTIFY</description></item>
        /// <item><description>None</description></item>
        /// </list>
        /// Note that NickServ CertFP is not on the autodetection list, as we have no means
        /// of verifying whether or not the remote NickServ supports CertFP. If the remote
        /// network does not have services, ensure that <see cref="AccountPassword"/> is <c>null</c>.
        /// </summary>
        public AuthType? AuthType { get; set; }

        /// <summary>
        /// Create a new Network that can be connected to.
        /// </summary>
        /// <param name="name">
        /// Name of the network, for the caller's internal tracking purposes.
        /// The name does not need to be unique.
        /// </param>
        /// <param name="primaryNick">Primary nickname to use when connecting to the network.</param>
        public Network(string name, string primaryNick)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(primaryNick);

            Name = name;
            PrimaryNick = primaryNick;
        }
    }
}
