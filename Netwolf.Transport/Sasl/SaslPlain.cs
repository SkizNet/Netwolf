using Netwolf.Transport.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        Username = username?.EncodeUtf8() ?? throw new ArgumentNullException(nameof(username));
        Impersonate = impersonate?.EncodeUtf8() ?? Array.Empty<byte>();
        Password = password?.EncodeUtf8() ?? throw new ArgumentNullException(nameof(password));
    }

    public bool Authenticate(ReadOnlySpan<byte> challenge, out ReadOnlySpan<byte> response)
    {
        if (++State > 1 || challenge.Length > 0)
        {
            response = Array.Empty<byte>();
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
}
