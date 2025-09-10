// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Unicode.PRECIS;

public static class CaseMapping
{
    /// <summary>
    /// Do not perform any case mapping.
    /// </summary>
    public static readonly Func<string, string>? None = null;

    /// <summary>
    /// Convert the string to lower case.
    /// </summary>
    public static readonly Func<string, string> ToLowerCase = input =>
    {
        // This isn't perfect (for example, it doesn't handle final sigma),
        // but it's good enough for IRC where usually only ASCII characters
        // are allowed in nicknames anyway (which is when case-folded profiles
        // come into play). If this ends up being important to get right,
        // open an issue on GitHub with your use case and I'll evaluate implementing
        // the full Unicode toLowerCase algorithm, including special cases.
        return input.ToLowerInvariant();
    };
}
