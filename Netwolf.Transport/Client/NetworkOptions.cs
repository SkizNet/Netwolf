using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Data class to encapsulate options for an <see cref="INetwork"/>
    /// </summary>
    public class NetworkOptions
    {
        private string? _ident;
        private string? _accountName;

        /// <summary>
        /// How long to wait before abandoning a connection to a particular <see cref="Server"/>.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How often to ping the remote <see cref="Server"/> to see if the connection is still live.
        /// </summary>
        public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// How long to wait for a ping reply before we consider the connection dead?
        /// </summary>
        public TimeSpan PingTimeout { get; set; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Primary nickname we attempt to use when connecting to this network
        /// </summary>
        /// <remarks>
        /// <seealso cref="SecondaryNick"/>
        /// </remarks>
        public string PrimaryNick { get; set; } = null!;

        /// <summary>
        /// Secondary nickname we attempt to use when connecting to this network,
        /// in the event <see cref="PrimaryNick"/> is in use.
        /// </summary>
        public string? SecondaryNick { get; set; }

        /// <summary>
        /// Ident to use. Defaults to <see cref="PrimaryNick"/> if not explicitly set.
        /// </summary>
        public string Ident
        {
            get => _ident ?? PrimaryNick;
            set => _ident = value;
        }

        /// <summary>
        /// List of servers to try when connecting (in order of preference).
        /// </summary>
        public List<Server> Servers { get; set; } = new List<Server>();

        /// <summary>
        /// Password required to connect to the network, if any.
        /// </summary>
        public string? ServerPassword { get; set; }

        /// <summary>
        /// Local host to bind to when connecting. If unset, use default outbound IP.
        /// </summary>
        public string? BindHost { get; set; }

        /// <summary>
        /// If true, disables certificate validation. This reduces security and should be used carefully. If true,
        /// the <see cref="TrustedFingerprints"/> and <see cref="CheckOnlineRevocation"/> settings have no effect.
        /// </summary>
        public bool AcceptAllCertificates { get; set; }

        /// <summary>
        /// A list of trusted certificate fingerprints. If this is defined, CA validation will not be performed,
        /// and the server's certificate must be hash to one of the fingerprints listed here. The format of entries
        /// should be hexidecimal SHA-256 fingerprints of certificates, with or without <c>:</c> divider characters.
        /// The strings are compared case-insensitively. If this list is not empty,
        /// <see cref="CheckOnlineRevocation"/> will have no effect.
        /// </summary>
        public List<string> TrustedFingerprints { get; set; } = new List<string>();

        /// <summary>
        /// If true, checks the certificate's OCSP responder for revocation information before accepting a connection.
        /// If the responder indicates that the certificate is revoked or the responder times out, the connection
        /// will be aborted.
        /// </summary>
        public bool CheckOnlineRevocation { get; set; }

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
        /// File path of TLS client certificate used to authenticate to the user's account.
        /// </summary>
        public string? AccountCertificateFile { get; set; }

        /// <summary>
        /// Password for the TLS client certificate, if any. This property will be set to <c>null</c>
        /// upon construction of a <see cref="Network"/> instance after it is bound from the configuration.
        /// </summary>
        public string? AccountCertificatePassword { get; set; }

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
        /// If set, and supported by the server, use CPRIVMSG and CNOTICE for outgoing messages
        /// when possible (i.e. when opped in a channel shared with the message targets).
        /// CPRIVMSG and CNOTICE bypass certain flood controls to make sending possibly a bit faster
        /// on the IRC network side, but can be disabled in case of incompatibilities client-side
        /// or a broken implementation network-side.
        /// </summary>
        public bool UseCPrivmsg { get; set; }
    }
}
