using Netwolf.Server.Users;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Internal;

/// <summary>
/// Account provider that does not support authentication, used when an username contains no
/// realm information and server configuration does not specify a default realm.
/// </summary>
internal class UnresolvedAccountProvider : IAccountProvider
{
    internal static readonly UnresolvedAccountProvider Instance = new();

    public string ProviderName => "Unknown";

    public IEnumerable<AuthMechanism> SupportedMechanisms => [];

    public Task<ClaimsIdentity?> AuthenticateCertAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ClaimsIdentity?> AuthenticatePlainAsync(byte[] username, byte[] password, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ClaimsIdentity?> AuthenticateScramAsync(byte[] username, byte[] nonce, byte[] hash, ImmutableDictionary<char, string> extensionData, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ScramParameters> GetScramParametersAsync(byte[] username, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
