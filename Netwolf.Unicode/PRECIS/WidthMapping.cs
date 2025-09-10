// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Unicode.Internal;

using System.Text;

namespace Netwolf.Unicode.PRECIS;

public static class WidthMapping
{
    /// <summary>
    /// Do not perform any width mapping.
    /// </summary>
    public static readonly Func<string, string>? None = null;

    /// <summary>
    /// Decompose fullwidth and halfwidth code points to their decomposition mappings
    /// according to the Unicode Standard Annex #11.
    /// </summary>
    public static readonly Func<string, string> Decompose = input =>
    {
        StringBuilder sb = new();

        foreach (var rune in input.EnumerateRunes())
        {
            sb.Append(DecompositionMappings.Decompose(rune, true, true));
        }

        return sb.ToString();
    };
}
