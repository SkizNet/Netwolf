using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

/// <summary>
/// Claim types used internally by Netwolf.
/// </summary>
public static class ServerClaimTypes
{
    /// <summary>
    /// Fully-qualified class name that generated this identity.
    /// </summary>
    public static readonly string IdentityProviderClass = "Netwolf.IdentityClass";

    /// <summary>
    /// Indicates whether the identity identifies a user account ("Account") or an oper ("Oper")
    /// </summary>
    public static readonly string IdentityType = "Netwolf.IdentityType";

    /// <summary>
    /// Friendly name of the identity provider, as defined in server configuration.
    /// </summary>
    public static readonly string IdentityProviderName = "Netwolf.IdentityProviderName";

    /// <summary>
    /// SASL mechanism name used to authenticate this identity. If SASL was not used,
    /// this should be the closest SASL mechanism to how the identity was authenticated,
    /// e.g. "PLAIN" for password-based authentication.
    /// </summary>
    public static readonly string IdentityMechanism = "Netwolf.IdentityMechanism";
}
