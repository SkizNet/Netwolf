// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.Commands;

public record CommandCreationOptions(int LineLen = 512, int ClientTagLen = 4096, int ServerTagLen = 8191);
