// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Collections.Immutable;

namespace Netwolf.Transport.IRC;

/// <summary>
/// Holds the state of the client's connection
/// </summary>
public sealed record IrcState(
    string Name,
    string Nick,
    string Ident,
    string Host,
    string? Account,
    ImmutableHashSet<char> UserModes,
    ImmutableDictionary<string, string?> SupportedCaps,
    ImmutableHashSet<string> EnabledCaps,
    ImmutableDictionary<ISupportToken, string?> ISupport
    ) : INetworkInfo
{
    public bool TryGetEnabledCap(string cap, out string? value)
    {
        value = SupportedCaps.GetValueOrDefault(cap);
        return EnabledCaps.Contains(cap);
    }

    public bool TryGetISupport(ISupportToken token, out string? value)
    {
        return ISupport.TryGetValue(token, out value);
    }

    public string? GetISupportOrDefault(ISupportToken token, string? defaultValue = null)
    {
        return ISupport.GetValueOrDefault(token, defaultValue);
    }
}
