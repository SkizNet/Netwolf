// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Netwolf.PRECIS;

public static class AdditionalMapping
{
    private static readonly Regex MultipleSpaceRegex = new(@" {2,}", RegexOptions.NonBacktracking);

    /// <summary>
    /// Do not perform any additional mapping.
    /// </summary>
    public static readonly Func<string, string>? None = null;

    /// <summary>
    /// Replace all non-ASCII spaces with ASCII space.
    /// </summary>
    public static readonly Func<string, string> MapSpaces = input =>
    {
        StringBuilder sb = new();

        foreach (var rune in input.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.SpaceSeparator)
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(rune.ToString());
            }
        }

        return sb.ToString();
    };

    /// <summary>
    /// Replace all non-ASCII spaces with ASCII space, trim ASCII spaces from the start and end,
    /// and replace multiple consecutive spaces inside the string with a single ASCII space.
    /// </summary>
    public static readonly Func<string, string> MapAndCollapseSpaces = input =>
    {
        return MultipleSpaceRegex.Replace(MapSpaces(input).Trim(' '), " ");
    };
}
