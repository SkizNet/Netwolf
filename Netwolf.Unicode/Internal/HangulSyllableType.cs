// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Unicode.Internal;

internal enum HangulSyllableType
{
    NA = 0x0000,
    NotApplicable = NA,
    L = 0x0001,
    LeadingJamo = L,
    V = 0x0002,
    VowelJamo = V,
    T = 0x0004,
    TrailingJamo = T,
    LV = 0x0008,
    LVSyllable = LV,
    LVT = 0x0010,
    LVTSyllable = LVT,
}
