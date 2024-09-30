using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.IRC;

public interface INetworkInfo
{
    /// <summary>
    /// Network name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Nickname for this connection
    /// </summary>
    string Nick { get; }

    /// <summary>
    /// Ident for this connection
    /// </summary>
    string Ident { get; }

    /// <summary>
    /// Hostname for this connection
    /// </summary>
    string Host { get; }

    /// <summary>
    /// Account name for this connection, or null if not logged in
    /// </summary>
    string? Account { get; }

    /// <summary>
    /// Attempt to get the value of a capability. The value is fetched regardless of whether the capability is enabled or not.
    /// </summary>
    /// <param name="cap">Capability name</param>
    /// <param name="value">Capability value (potentially null if the ircd didn't present a value for this capability)</param>
    /// <returns>true if the capability is enabled, false if it is not</returns>
    bool TryGetEnabledCap(string cap, out string? value);

    /// <summary>
    /// Attempt to get the value of an ISUPPORT token.
    /// </summary>
    /// <param name="token">Token name</param>
    /// <param name="value">Token value (potentially null if the ircd didn't present a value for this token)</param>
    /// <returns>true if the token was given by the ircd, false if it was not</returns>
    bool TryGetISupport(ISupportToken token, out string? value);
}
