// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Unicode.Internal;

internal enum EastAsianWidth
{
    N = 0,
    Neutral = N,
    F = 1,
    FullWidth = F,
    H = 2,
    HalfWidth = H,
    W = 3,
    Wide = W,
    Na = 4,
    Narrow = Na,
    A = 5,
    Ambiguous = A,
}
