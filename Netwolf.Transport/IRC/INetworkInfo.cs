// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

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
    /// Channel types supported by the ircd. If an empty string, the ircd supposedly doesn't support channels.
    /// </summary>
    string ChannelTypes => GetISupportOrDefault(ISupportToken.CHANTYPES, "#&") ?? string.Empty;

    /// <summary>
    /// Case mapping in use by the ircd. Unrecognized values are coerced to ascii.
    /// </summary>
    CaseMapping CaseMapping => GetISupportOrDefault(ISupportToken.CASEMAPPING, "ascii") switch
    {
        "ascii" => CaseMapping.Ascii,
        "rfc1459" => CaseMapping.Rfc1459,
        "rfc1459-strict" => CaseMapping.Rfc1459Strict,
        _ => CaseMapping.Ascii,
    };

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

    /// <summary>
    /// Retrieve the value of the given ISUPPORT token, or the <paramref name="defaultValue"/> if that token was not
    /// specified by the ircd. The default value is NOT used if the token is present but lacks a value; null will be returned in such cases.
    /// </summary>
    /// <param name="token">Token name</param>
    /// <param name="defaultValue">Default value to use if the token was not given by the ircd.</param>
    /// <returns></returns>
    string? GetISupportOrDefault(ISupportToken token, string? defaultValue = null);
}
