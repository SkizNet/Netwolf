using Netwolf.Transport.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
