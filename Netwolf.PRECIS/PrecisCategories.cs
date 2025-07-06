// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PRECIS;

/// <summary>
/// Unicode character categories used in PRECIS classes and profiles.
/// </summary>
[Flags]
public enum PrecisCategories : int
{
    LetterDigits              = 0x0000_0001,
    Unstable                  = 0x0000_0002,
    IgnorableProperties       = 0x0000_0004,
    IgnorableBlocks           = 0x0000_0008,
    Ldh                       = 0x0000_0010,
    Exceptions                = 0x0000_0020,
    BackwardCompatible        = 0x0000_0040,
    JoinControl               = 0x0000_0080,
    OldHangulJamo             = 0x0000_0100,
    Unassigned                = 0x0000_0200,
    Ascii7                    = 0x0000_0400,
    Controls                  = 0x0000_0800,
    PrecisIgnorableProperties = 0x0000_1000,
    Spaces                    = 0x0000_2000,
    Symbols                   = 0x0000_4000,
    Punctuation               = 0x0000_8000,
    HasCompat                 = 0x0001_0000,
    OtherLetterDigits         = 0x0002_0000,
    // Composed flags to make it easier to test
    PVALID                    = 0x0100_0000,
    CONTEXTJ                  = 0x0200_0000,
    CONTEXTO                  = 0x0400_0000,
    DISALLOWED                = 0x0800_0000,
    UNASSIGNED                = 0x1000_0000,
    ID_DIS                    = 0x2000_0000,
    FREE_PVAL                 = 0x4000_0000,
}
