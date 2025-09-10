// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Unicode;

namespace Netwolf.Transport.Sasl;

public sealed class SaslExternal : ISaslMechanism
{
    public string Name => "EXTERNAL";

    private byte[] Username { get; init; }

    public SaslExternal(string? username)
    {
        Username = username?.EncodeUtf8() ?? Array.Empty<byte>();
    }

    public bool Authenticate(ReadOnlySpan<byte> challenge, out ReadOnlySpan<byte> response)
    {
        if (challenge.Length > 0)
        {
            response = Array.Empty<byte>();
            return false;
        }

        response = Username;
        return true;
    }

    public void Dispose()
    {
        // nothing to do here
    }
}
