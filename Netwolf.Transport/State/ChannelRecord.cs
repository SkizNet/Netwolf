// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Collections.Immutable;
using System.Diagnostics;

namespace Netwolf.Transport.State;

/// <summary>
/// Contains identifying information on a channel.
/// </summary>
[DebuggerDisplay("{Name} ({ModesDisplay}) {OpCount} ops, {Users.Count} total")]
public sealed record ChannelRecord(
    Guid Id,
    string Name,
    string Topic,
    ImmutableDictionary<char, string?> Modes,
    ImmutableDictionary<Guid, string> Users
    )
{
    /// <summary>
    /// Determines the number of channel operators in the channel.
    /// This does not use the network's PREFIX settings, instead it hardcodes the standard op, admin/protected, and owner/founder modes.
    /// Halfops and voice are not counted. If the network has nonstandard PREFIXes, this may not be accurate.
    /// </summary>
    public int OpCount => Users.Count(u => u.Value.Contains('@') || u.Value.Contains('&') || u.Value.Contains('~'));

    /// <summary>
    /// Channel modes including parameters, or empty string if none.
    /// </summary>
    public string ModesDisplay
    {
        get
        {
            if (Modes.Count == 0)
            {
                return string.Empty;
            }

            List<string> modes = [string.Concat(Modes.Keys.OrderBy(m => m))];
            foreach (var param in Modes.OrderBy(m => m.Key).Select(m => m.Value))
            {
                if (param != null)
                {
                    modes.Add(param);
                }
            }

            return $"+{string.Join(" ", modes)}";
        }
    }
}
