// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PRECIS.Internal;

internal enum JoiningType
{
    R = 0x0001,
    RightJoining = R,
    L = 0x0002,
    LeftJoining = L,
    D = 0x0004,
    DualJoining = D,
    C = 0x0008,
    JoinCausing = C,
    U = 0x0010,
    NonJoining = U,
    T = 0x0020,
    Transparent = T,
}
