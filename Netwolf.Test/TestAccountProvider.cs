using Netwolf.Server.Users;
using Netwolf.Transport.Extensions;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test;

internal class TestAccountProvider : IAccountProvider
{
    public string ProviderName => "Test";

    public IEnumerable<AuthMechanism> SupportedMechanisms => [
        AuthMechanism.Password,
        AuthMechanism.Scram,
        AuthMechanism.Impersonate,
    ];

    public Task<ClaimsIdentity?> AuthenticateCertAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ClaimsIdentity?> AuthenticatePlainAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (username == "foo" && password == "bar")
        {
            return Task.FromResult<ClaimsIdentity?>(new ClaimsIdentity(
                [ new(ClaimTypes.Name, username) ],
                "Password",
                ClaimTypes.Name,
                ClaimTypes.Role
            ));
        }

        return Task.FromResult<ClaimsIdentity?>(null);
    }

    public Task<ClaimsIdentity?> AuthenticateScramAsync(string username, string nonce, byte[] hash, ImmutableDictionary<char, string> extensionData, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ScramParameters?> GetScramParametersAsync(string username, CancellationToken cancellationToken)
    {
        return Task.FromResult<ScramParameters?>(username switch
        {
            // iteration counts set to the minimum recommended by their respective RFCs
            "s1" => new(HashAlgorithmName.SHA1, "salt-sha1"u8.ToImmutableArray(), 4096, ScramParameters.NoExtensions),
            "s256" => new(HashAlgorithmName.SHA256, "salt-sha256"u8.ToImmutableArray(), 4096, ScramParameters.NoExtensions),
            "s512" => new(HashAlgorithmName.SHA512, "salt-sha512"u8.ToImmutableArray(), 4096, ScramParameters.NoExtensions),
            "s3512" => new(HashAlgorithmName.SHA3_512, "salt-sha3512"u8.ToImmutableArray(), 10000, ScramParameters.NoExtensions),
            // NOTE: move the following outside of here; we don't need to involve Netwolf.Server to test the client side of SCRAM for invalid formats
            // these tests are mostly for the server side implementation
            // reject iteration counts that are too low (less than the recommended minimum)
            "baditer1" => new(HashAlgorithmName.SHA1, "salt-sha1"u8.ToImmutableArray(), 4095, ScramParameters.NoExtensions),
            "baditer256" => new(HashAlgorithmName.SHA256, "salt-sha256"u8.ToImmutableArray(), 4095, ScramParameters.NoExtensions),
            "baditer512" => new(HashAlgorithmName.SHA512, "salt-sha512"u8.ToImmutableArray(), 4095, ScramParameters.NoExtensions),
            "baditer3512" => new(HashAlgorithmName.SHA3_512, "salt-sha3512"u8.ToImmutableArray(), 9999, ScramParameters.NoExtensions),
            // reject unsalted passwords
            "badsalt" => new(HashAlgorithmName.SHA256, [], 4096, ScramParameters.NoExtensions),
            // reject mandatory extensions
            "badext" => new(HashAlgorithmName.SHA256, "salt-sha256"u8.ToImmutableArray(), 4096, ScramParameters.MakeExtensionData(('m', "value"))),
            // default null indicates unrecognized username
            _ => null
        });
    }

    public Task<ClaimsIdentity?> ImpersonateAsync(string username, CancellationToken cancellationToken)
    {
        return Task.FromResult<ClaimsIdentity?>(new ClaimsIdentity(
            [new(ClaimTypes.Name, username)],
            "Impersonate",
            ClaimTypes.Name,
            ClaimTypes.Role
        ));
    }
}
