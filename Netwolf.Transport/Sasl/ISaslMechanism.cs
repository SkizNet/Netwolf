// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Security.Authentication.ExtendedProtection;

namespace Netwolf.Transport.Sasl;

public interface ISaslMechanism : IDisposable
{
    string Name { get; }

    /// <summary>
    /// For SASL mechanisms that use TLS channel binding, set this to true.
    /// </summary>
    bool SupportsChannelBinding => false;

    bool Authenticate(ReadOnlySpan<byte> challenge, out ReadOnlySpan<byte> response);

    /// <summary>
    /// For SASL mechanisms that use TLS channel binding, override this to capture the binding data.
    /// </summary>
    /// <param name="uniqueData"></param>
    /// <param name="endpointData"></param>
    /// <returns>true if we accept the data, false if this mechanism cannot work with the passed-in data</returns>
    bool SetChannelBindingData(ChannelBindingKind kind, ChannelBinding? data)
    {
        throw new NotImplementedException();
    }
}
