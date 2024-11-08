// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Extensions;

using System.Security.Cryptography;

namespace Netwolf.Transport.Sasl;

public sealed class SaslPlain : ISaslMechanism
{
    public string Name => "PLAIN";

    private byte[] Username { get; init; }

    private byte[] Impersonate { get; init; }

    private byte[] Password { get; init; }

    private int State { get; set; } = 0;

    public SaslPlain(string username, string? impersonate, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        Username = username.EncodeUtf8();
        Impersonate = impersonate?.EncodeUtf8() ?? [];
        Password = password.EncodeUtf8();
    }

    public bool Authenticate(ReadOnlySpan<byte> challenge, out ReadOnlySpan<byte> response)
    {
        if (++State > 1 || challenge.Length > 0)
        {
            response = [];
            return false;
        }

        var buffer = new byte[Impersonate.Length + Username.Length + Password.Length + 2];

        // start at 1 to account for the null separator after authzid (which may or may not exist)
        int offset = 1;

        if (Impersonate.Length > 0)
        {
            Array.Copy(Impersonate, buffer, Impersonate.Length);
            offset += Impersonate.Length;
        }

        // authcid
        Array.Copy(Username, 0, buffer, offset, Username.Length);

        // account for null between authcid and passwd by adding 1 more to offset
        offset += Username.Length + 1;

        // passwd
        Array.Copy(Password, 0, buffer, offset, Password.Length);

        response = buffer;
        return true;
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Password);
    }
}
