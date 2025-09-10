// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Unicode.PRECIS;

[Flags]
public enum PrecisRules : byte
{
    WidthMapping = 0x01,
    AdditionalMapping = 0x02,
    CaseMapping = 0x04,
    Normalization = 0x08,
    Directionality = 0x10,
    Behavioral = 0x20,
    // All intentionally excludes ForbidEmpty; it's meant to apply all "regular" rules
    All = WidthMapping | AdditionalMapping | CaseMapping | Normalization | Directionality | Behavioral,
    ForbidEmpty = 0x40,
}
