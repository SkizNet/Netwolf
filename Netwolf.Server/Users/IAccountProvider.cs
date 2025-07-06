using Netwolf.Transport.Extensions;

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
    string ProviderName { get; }

    IEnumerable<AuthMechanism> SupportedMechanisms { get; }

    /// <summary>
    /// Given a string that is potentially of the form "username@realm", extracts
    /// the username part from the string. If the string does not contain a '@' character,
    /// it returns the string as is. No normalization is performed; use the methods
    /// found in Netwolf.PRECIS if normalization is needed.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    string ExtractUsername(ReadOnlySpan<byte> username)
    {
        var at = username.IndexOf((byte)'@');
        return (at == -1 ? username : username[..at]).DecodeUtf8();
    }

    Task<ClaimsIdentity?> AuthenticatePlainAsync(string username, string password, CancellationToken cancellationToken);

    Task<ScramParameters?> GetScramParametersAsync(string username, CancellationToken cancellationToken);

    Task<ClaimsIdentity?> AuthenticateScramAsync(string username, string nonce, byte[] hash, ImmutableDictionary<char, string> extensionData, CancellationToken cancellationToken);

    Task<ClaimsIdentity?> AuthenticateCertAsync(X509Certificate2 certificate, CancellationToken cancellationToken);

    Task<ClaimsIdentity?> ImpersonateAsync(string username, CancellationToken cancellationToken);
}
