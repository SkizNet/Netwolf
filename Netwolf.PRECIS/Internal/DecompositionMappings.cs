// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text;

namespace Netwolf.PRECIS.Internal;

internal static partial class DecompositionMappings
{
    // Defined in source generator
    private static partial IEnumerable<KeyValuePair<int, int[]>> GetWideMappings();
    private static partial IEnumerable<KeyValuePair<int, int[]>> GetNarrowMappings();

    private static readonly Dictionary<int, string> WideMappings;
    private static readonly Dictionary<int, string> NarrowMappings;

    static DecompositionMappings()
    {
        WideMappings = new(GetWideMappings().Select(ConvertToLookup));
        NarrowMappings = new(GetNarrowMappings().Select(ConvertToLookup));
    }

    private static KeyValuePair<int, string> ConvertToLookup(KeyValuePair<int, int[]> mapping)
    {
        StringBuilder sb = new();
        foreach (var cp in mapping.Value)
        {
            sb.Append(new Rune(cp).ToString());
        }

        return new(mapping.Key, sb.ToString());
    }

    internal static string Decompose(Rune character, bool doWideDecomposition, bool doNarrowDecomposition)
    {
        if (doWideDecomposition && WideMappings.TryGetValue(character.Value, out var wide))
        {
            return wide;
        }

        if (doNarrowDecomposition && NarrowMappings.TryGetValue(character.Value, out var narrow))
        {
            return narrow;
        }

        return character.ToString();
    }
}
