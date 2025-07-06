// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PRECIS.Internal;

/// <summary>
/// Valid values are 0-254; only values that we make use of (plus the default) elsewhere
/// are included in the enum. <see cref="UnicodeProperty.GetCombiningClass(System.Text.Rune)"/>
/// may return values not be explicitly listed in this enum in that 0..254 range.
/// </summary>
internal enum CombiningClass
{
    NotReordered = 0,
    Virama = 9,
}
