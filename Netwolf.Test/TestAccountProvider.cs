using Netwolf.Server.Users;
using Netwolf.Transport.Extensions;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test;

internal class TestAccountProvider : IAccountProvider
{
    public string ProviderName => "Test";

    public IEnumerable<AuthMechanism> SupportedMechanisms => [
        AuthMechanism.Password
    ];

    public Task<ClaimsIdentity?> AuthenticateCertAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ClaimsIdentity?> AuthenticatePlainAsync(byte[] username, byte[] password, CancellationToken cancellationToken)
    {
        string u = username.DecodeUtf8();
        string p = password.DecodeUtf8();

        if (u == "foo" && p == "bar")
        {
            return Task.FromResult<ClaimsIdentity?>(new ClaimsIdentity(
                [ new(ClaimTypes.Name, u) ],
                "Password",
                ClaimTypes.Name,
                ClaimTypes.Role
            ));
        }

        return Task.FromResult<ClaimsIdentity?>(null);
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
