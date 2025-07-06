// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text;

namespace Netwolf.PRECIS.Internal;

internal static partial class UnicodeProperty
{
    internal static partial bool IsJoinControl(Rune rune);

    internal static partial bool IsNoncharacterCodePoint(Rune rune);

    internal static partial bool IsDefaultIgnorableCodePoint(Rune rune);

    internal static partial HangulSyllableType GetHangulSyllableType(Rune rune);

    internal static partial BidiClass GetBidiClass(Rune rune);

    internal static partial CombiningClass GetCombiningClass(Rune rune);

    internal static partial JoiningType GetJoiningType(Rune rune);

    internal static partial Script GetScript(Rune rune);

    private static T? GeneratedDatabaseLookup<T>(int value, List<Tuple<int, int, T>> database)
        where T : struct
    {
        int start = 0;
        int end = database.Count;
        int cur = end >> 1;

        if (value < 0 || value > 0x10FFFD)
        {
            // outside of unicode range; should never happen (and indicates a bug somewhere if it does)
            throw new ArgumentException("Value outside of Unicode range(0x0000 - 0x10FFFD)", nameof(value));
        }

        while (end > start)
        {
            if (value < database[cur].Item1)
            {
                end = cur - 1;
            }
            else if (value > database[cur].Item2)
            {
                start = cur + 1;
            }
            else
            {
                return database[cur].Item3;
            }

            cur = (start + end) >> 1;
        }

        return null;
    }
}
