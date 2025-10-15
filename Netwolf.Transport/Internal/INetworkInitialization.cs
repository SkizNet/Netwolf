// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Internal;

/// <summary>
/// Interface for command listeners that need to perform network-specific initialization.
/// </summary>
internal interface INetworkInitialization
{
    void InitializeForNetwork(INetwork network);
}
