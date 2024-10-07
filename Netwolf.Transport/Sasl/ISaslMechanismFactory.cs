// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Sasl;

/// <summary>
/// Factory for <see cref="ISaslMechanism"/>, registered as a DI service
/// </summary>
public interface ISaslMechanismFactory
{
    /// <summary>
    /// Get supported SASL mechanisms, in order of preference
    /// </summary>
    /// <param name="options">Network options for the current connection</param>
    /// <param name="server">Server we are currently connected to</param>
    /// <returns></returns>
    IEnumerable<string> GetSupportedMechanisms(NetworkOptions options, IServer server);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="mechanism"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    ISaslMechanism CreateMechanism(string mechanism, NetworkOptions options);
}
