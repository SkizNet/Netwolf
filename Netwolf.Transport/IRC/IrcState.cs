// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

/// <summary>
/// Holds the state of the client's connection
/// </summary>
public class IrcState : INetworkInfo
{
    public string Name { get; internal set; } = default!;

    /// <summary>
    /// The current nickname for the connection
    /// </summary>
    public string Nick { get; internal set; } = default!;

    /// <summary>
    /// Our ident for this connection
    /// </summary>
    public string Ident { get; internal set; } = default!;

    /// <summary>
    /// Our host / vhost for this connection
    /// </summary>
    public string Host { get; internal set; } = default!;

    /// <summary>
    /// Our account for this connection, or <c>null</c> if we don't have one
    /// </summary>
    public string? Account { get; internal set; }

    internal Dictionary<string, string?> SupportedCaps { get; init; } = [];

    internal HashSet<string> EnabledCaps { get; init; } = [];

    internal Dictionary<ISupportToken, string?> ISupport { get; init; } = [];

    public bool TryGetEnabledCap(string cap, out string? value)
    {
        value = SupportedCaps.GetValueOrDefault(cap);
        return EnabledCaps.Contains(cap);
    }

    public bool TryGetISupport(ISupportToken token, out string? value)
    {
        return ISupport.TryGetValue(token, out value);
    }
}
