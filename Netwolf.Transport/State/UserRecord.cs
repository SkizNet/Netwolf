// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Collections.Immutable;
using System.Diagnostics;

namespace Netwolf.Transport.State;

/// <summary>
/// Contains information on a user.
/// </summary>
[DebuggerDisplay("{Nick}!{Ident}@{Host} <{AccountDisplay}> ({ModesDisplay})")]
public sealed record UserRecord(
    Guid Id,
    string Nick,
    string Ident,
    string Host,
    string? Account,
    bool IsAway,
    string RealName,
    ImmutableHashSet<char> Modes,
    ImmutableDictionary<Guid, string> Channels
    )
{
    /// <summary>
    /// Account name for this user, or "*" if not logged in.
    /// </summary>
    public string AccountDisplay => Account ?? "*";

    /// <summary>
    /// User modes for this user; empty string if none.
    /// </summary>
    public string ModesDisplay => Modes.Count == 0 ? string.Empty : $"+{string.Join("", Modes.OrderBy(m => m))}";
}
