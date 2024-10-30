// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Internal;

using System.Collections.Immutable;

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
    /// User modes for this connection
    /// </summary>
    ImmutableHashSet<char> UserModes { get; }

    /// <summary>
    /// Channel types supported by the ircd. If an empty string, the ircd supposedly doesn't support channels.
    /// </summary>
    string ChannelTypes => GetISupportOrDefault(ISupportToken.CHANTYPES, ISupportDefaults.DefaultChannelTypes) ?? string.Empty;

    /// <summary>
    /// Channel modes that operate as lists (aka "type A" modes).
    /// </summary>
    string ChannelModesA => GetISupportOrDefault(ISupportToken.CHANMODES, ISupportDefaults.DefaultChannelModes)?.Split(',').ElementAtOrDefault(0) ?? string.Empty;

    /// <summary>
    /// Channel modes that require a value when setting and unsetting (aka "type B" modes).
    /// This list <strong>does not</strong> include "prefix" modes. See <see cref="ChannelPrefixModes"/> for those.
    /// </summary>
    string ChannelModesB => GetISupportOrDefault(ISupportToken.CHANMODES, ISupportDefaults.DefaultChannelModes)?.Split(',').ElementAtOrDefault(1) ?? string.Empty;

    /// <summary>
    /// Channel modes that require a value when setting but not when unsetting (aka "type C" modes).
    /// </summary>
    string ChannelModesC => GetISupportOrDefault(ISupportToken.CHANMODES, ISupportDefaults.DefaultChannelModes)?.Split(',').ElementAtOrDefault(2) ?? string.Empty;

    /// <summary>
    /// Channel modes that do not require values (aka "type D" modes).
    /// </summary>
    string ChannelModesD => GetISupportOrDefault(ISupportToken.CHANMODES, ISupportDefaults.DefaultChannelModes)?.Split(',').ElementAtOrDefault(3) ?? string.Empty;

    /// <summary>
    /// Prefix modes supported by the ircd, in order from highest status to lowest status.
    /// If an empty string, the ircd supposedly doesn't support channel status.
    /// </summary>
    string ChannelPrefixModes => string.Concat((GetISupportOrDefault(ISupportToken.PREFIX, ISupportDefaults.DefaultPrefix) ?? string.Empty).Skip(1).TakeWhile(static c => c != ')'));

    /// <summary>
    /// Prefix symbols supported by the ircd, in order from highest status to lowest status.
    /// If an empty string, the ircd supposedly doesn't support channel status.
    /// </summary>
    string ChannelPrefixSymbols => string.Concat((GetISupportOrDefault(ISupportToken.PREFIX, ISupportDefaults.DefaultPrefix) ?? string.Empty).SkipWhile(static c => c != ')').Skip(1));

    /// <summary>
    /// Case mapping in use by the ircd. Unrecognized values are coerced to ascii.
    /// </summary>
    CaseMapping CaseMapping => GetISupportOrDefault(ISupportToken.CASEMAPPING, ISupportDefaults.DefaultCasemapping) switch
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
