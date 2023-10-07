using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.MFA;

public interface IMfaMechanism
{
    /// <summary>
    /// Friendly (human-facing) name for this mechanism.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Name for this mechanism as used in SCRAM; all-lowercase
    /// </summary>
    string ScramName { get; }

    /// <summary>
    /// Provider-specific token used to prevent replay attacks.
    /// </summary>
    object? ReplayToken { get; }

    /// <summary>
    /// Generate a token for this mechanism, for use client-side
    /// </summary>
    /// <param name="challenge">Optional challenge, for things that require it</param>
    /// <returns>Token as a string</returns>
    Task<string> GetTokenAsync(byte[]? challenge = null);

    /// <summary>
    /// Validate a token for this mechanism, for use server-side.
    /// If successful, <see cref="ReplayToken"/> will be updated.
    /// </summary>
    /// <param name="token">Token provided by the client</param>
    /// <param name="challenge">Optional challenge, for things that require it</param>
    /// <returns>Whether token is valid or not</returns>
    Task<bool> ValidateAsync(string token, byte[]? challenge = null);
}
