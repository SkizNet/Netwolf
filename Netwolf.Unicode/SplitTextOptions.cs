// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Unicode;

/// <summary>
/// Options that control behavior of <see cref="LineBreakHelper.SplitText(string, int, Netwolf.Unicode.SplitTextOptions)"/>
/// </summary>
[Flags]
public enum SplitTextOptions
{
    /// <summary>
    /// Do not apply any special options
    /// </summary>
    None = 0,
    /// <summary>
    /// Allow lines to exceed the maximum length if no break opportunities are found.
    /// By default, a line will be broken at or before the maximum length to ensure that length is never exceeded,
    /// even if that means breaking in an inopportune location.
    /// </summary>
    AllowOverflow = 1,
    /// <summary>
    /// Indicates that break characters, such as CR or LF, should be included in the returned lines.
    /// By default, these characters are omitted.
    /// </summary>
    IncludeBreakCharacters = 2,
}
