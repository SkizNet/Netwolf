using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

/// <summary>
/// Service defining an account/identity provider.
/// Methods for unsupported mechanisms should throw NotImplementedException.
/// </summary>
public interface IAccountProvider
{
    IEnumerable<AuthMechanism> SupportedMechanisms { get; }

    Task<ClaimsIdentity?> AuthenticatePlainAsync(byte[] username, byte[] password, CancellationToken cancellationToken);

    Task<ScramParameters> GetScramParametersAsync(byte[] username, CancellationToken cancellationToken);

    Task<ClaimsIdentity?> AuthenticateScramAsync(byte[] username, byte[] nonce, byte[] hash, ImmutableDictionary<char, string> extensionData, CancellationToken cancellationToken);

    Task<ClaimsIdentity?> AuthenticateCertAsync(X509Certificate2 certificate, CancellationToken cancellationToken);
}
