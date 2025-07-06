// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PRECIS.Internal;

internal enum BidiClass : int
{
    // Bit flags for easier tests
    AllowedInLtr = 0x0001_0000,
    AllowedInRtl = 0x0002_0000,
    LtrFinisher = 0x0004_0000,
    RtlFinisher = 0x0008_0000,
    Strong = 0x0010_0000,
    Weak = 0x0020_0000,
    Neutral = 0x0040_0000,
    ExplicitFormatting = 0x0080_0000,
    // Strong types
    L = 0x0015_0001,
    LeftToRight = L,
    R = 0x001A_0002,
    RightToLeft = R,
    AL = 0x001A_0003,
    ArabicLetter = AL,
    // Weak types
    EN = 0x002F_0004,
    EuropeanNumber = EN,
    ES = 0x0023_0005,
    EuropeanSeparator = ES,
    ET = 0x0023_0006,
    EuropeanTerminator = ET,
    AN = 0x002A_0007,
    ArabicNumber = AN,
    CS = 0x0023_0008,
    CommonSeparator = CS,
    NSM = 0x0023_0009,
    NonspacingMark = NSM,
    BN = 0x0023_000A,
    BoundaryNeutral = BN,
    // Neutral types
    B = 0x0040_000B,
    ParagraphSeparator = B,
    S = 0x0040_000C,
    SegmentSeparator = S,
    WS = 0x0040_000D,
    WhiteSpace = WS,
    ON = 0x0043_000E,
    OtherNeutral = ON,
    // Explicit Formatting types
    LRE = 0x0080_000F,
    LeftToRightEmbedding = LRE,
    LRO = 0x0080_0010,
    LeftToRightOverride = LRO,
    RLE = 0x0080_0011,
    RightToLeftEmbedding = RLE,
    RLO = 0x0080_0012,
    RightToLeftOverride = RLO,
    PDF = 0x0080_0013,
    PopDirectionalFormat = PDF,
    LRI = 0x0080_0014,
    LeftToRightIsolate = LRI,
    RLI = 0x0080_0015,
    RightToLeftIsolate = RLI,
    FSI = 0x0080_0016,
    FirstStrongIsolate = FSI,
    PDI = 0x0080_0017,
    PopDirectionalIsolate = PDI,
}
