using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Defines the security settings for a <see cref="Connection"/>.
    /// </summary>
    public class SecurityPolicy
    {
        /// <summary>
        /// If true, TLS is used for this connection. The rest of these settings have no effect if this is false.
        /// </summary>
        public bool Secure { get; set; }

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
        /// If defined, this certificate will be presented as a TLS client certificate when initiating the connection.
        /// This is useful for SASL EXTERNAL / CertFP authentication.
        /// </summary>
        public X509Certificate? ClientCertificate { get; set; }
    }
}
