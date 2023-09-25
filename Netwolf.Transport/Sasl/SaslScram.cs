using Netwolf.Transport.Extensions;

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.Transport.Sasl;

public sealed class SaslScram : ISaslMechanism
{
    public string Name { get; init; }

    bool ISaslMechanism.SupportsChannelBinding => true;

    private ChannelBinding? UniqueBindingData { get; set; }

    private ChannelBinding? EndpointBindingData { get; set; }

    private Func<byte[], byte[]> Hash { get; init; }

    private Func<byte[], byte[], byte[]> Hmac { get; init; }

    private int HashSize { get; init; }

    private string Username { get; init; }

    private string? Impersonate { get; init; }

    private byte[] Password { get; init; }

    private bool Plus { get; init; }

    private int State { get; set; } = 0;

    private string Nonce { get; set; }

    private string ClientHeader { get; set; } = null!;

    private string ClientFirstMessageBare { get; set; } = null!;

    private string ExpectedServerSignature { get; set; } = null!;

    public SaslScram(string hashName, string username, string? impersonate, string password, bool plus = false)
    {
        if (hashName == null)
        {
            throw new ArgumentNullException(nameof(hashName));
        }

        switch (hashName)
        {
            case "SHA-1":
                Hash = SHA1.HashData;
                Hmac = HMACSHA1.HashData;
                HashSize = SHA1.HashSizeInBytes;
                break;
            case "SHA-256":
                Hash = SHA256.HashData;
                Hmac = HMACSHA256.HashData;
                HashSize = SHA256.HashSizeInBytes;
                break;
            default:
                throw new ArgumentException("Unsupported hash name", nameof(hashName));
        }

        Name = "SCRAM-" + hashName + (plus ? "-PLUS" : String.Empty);
        Username = username?.Normalize(NormalizationForm.FormKC).Replace("=", "=3D").Replace(",", "=2C")
            ?? throw new ArgumentNullException(nameof(username));
        Impersonate = impersonate?.Normalize(NormalizationForm.FormKC).Replace("=", "=3D").Replace(",", "=2C");
        // password gets normalized but doesn't have the replacements for = and ,
        Password = password?.Normalize(NormalizationForm.FormKC).EncodeUtf8()
            ?? throw new ArgumentNullException(nameof(password));
        // 128-bit random nonce (trailing ='s are unnecessary and don't add to security of nonce)
        Nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=');
    }

    // This needs some refactoring into helper methods
    public bool Authenticate(ReadOnlySpan<byte> challenge, out ReadOnlySpan<byte> response)
    {
        if (Plus && UniqueBindingData == null && EndpointBindingData == null)
        {
            // can't bind
            response = Array.Empty<byte>();
            return false;
        }

        // all fields recognized by the SCRAM standard
        string recognized = "anmrcsipve";

        State++;
        if (State == 1)
        {
            if (challenge.Length > 0)
            {
                response = Array.Empty<byte>();
                return false;
            }

            StringBuilder sb = new();
            // GS2 header
            string header;

            if (UniqueBindingData == null && EndpointBindingData == null)
            {
                header = "n,";
            }
            else if (!Plus)
            {
                header = "y,";
            }
            else if (UniqueBindingData != null)
            {
                header = "p=tls-unique,";
            }
            else
            {
                header = "p=tls-server-end-point,";
            }

            if (Impersonate != null)
            {
                header += $"a={Impersonate},";
            }
            else
            {
                header += ",";
            }

            // username
            sb.Append("n=");
            sb.Append(Username);

            // nonce
            sb.Append(",r=");
            sb.Append(Nonce);

            response = (header + sb.ToString()).EncodeUtf8();
            ClientHeader = header;
            ClientFirstMessageBare = sb.ToString();
            return true;
        }
        else if (State == 2)
        {
            var serverFirstMessage = challenge.DecodeUtf8();
            var serverResponse = serverFirstMessage.Split(',');
            int iterations = 0;
            byte[]? salt = null;

            if (serverResponse.Length < 3)
            {
                response = Array.Empty<byte>();
                return false;
            }

            for (var i = 0; i < serverResponse.Length; ++i)
            {
                var piece = serverResponse[i];
                var d = piece.Split('=', 2);
                if (d.Length != 2 || d[0].Length != 1 || !Char.IsAsciiLetter(d[0][0]))
                {
                    response = Array.Empty<byte>();
                    return false;
                }

                if (i == 0 && d[0] == "r")
                {
                    // nonce doesn't start with our client-specified one,
                    // or server didn't add on their own nonce to the end?
                    if (!d[1].StartsWith(Nonce) || d[1] == Nonce)
                    {
                        response = Array.Empty<byte>();
                        return false;
                    }

                    Nonce = d[1];
                }
                else if (i == 0 && d[0] == "m")
                {
                    // we don't support mandatory extensions
                    response = Array.Empty<byte>();
                    return false;
                }
                else if (i == 1 && d[0] == "s")
                {
                    try
                    {
                        salt = Convert.FromBase64String(d[1]);
                    }
                    catch (FormatException)
                    {
                        // invalid base64
                        response = Array.Empty<byte>();
                        return false;
                    }

                    // missing salt?
                    if (salt.Length == 0)
                    {
                        response = Array.Empty<byte>();
                        return false;
                    }
                }
                else if (i == 2 && d[0] == "i")
                {
                    // try to parse the number
                    if (!Int32.TryParse(d[1], out iterations))
                    {
                        response = Array.Empty<byte>();
                        return false;
                    }

                    // number invalid?
                    if (iterations < 1)
                    {
                        response = Array.Empty<byte>();
                        return false;
                    }
                }
                else if (i >= 3 && !recognized.Contains(d[0][0]))
                {
                    // optional extensions; ignore
                }
                else
                {
                    // invalid / ill-formed server challenge
                    response = Array.Empty<byte>();
                    return false;
                }
            }

            StringBuilder sb = new();

            // base64-encoded GS2 header + channel binding data
            var gs2Header = ClientHeader.EncodeUtf8();
            var bindingData = UniqueBindingData ?? EndpointBindingData;
            int dataSize = (Plus && bindingData != null) ? bindingData.Size : 0;
            byte[] buffer = new byte[gs2Header.Length + dataSize];
            Array.Copy(gs2Header, buffer, gs2Header.Length);

            if (Plus && bindingData != null)
            {
                bool success = false;
                try
                {
                    bindingData.DangerousAddRef(ref success);
                }
                catch (ObjectDisposedException)
                {
                    // swallow exception; success remains set to false
                }

                if (!success)
                {
                    // channel binding data got deallocated?
                    response = Array.Empty<byte>();
                    return false;
                }

                Marshal.Copy(bindingData.DangerousGetHandle(), buffer, gs2Header.Length, bindingData.Size);
                bindingData.DangerousRelease();
            }

            sb.Append("c=");
            sb.Append(Convert.ToBase64String(buffer));

            // nonce
            sb.Append(",r=");
            sb.Append(Nonce);

            // proof

            // last 4 bytes of initial are {0, 0, 0, 1} (the integer 1 in big endian byte order)
            // new byte[] initializes everything to 0, so we just need to set the 1.
            var initial = new byte[salt!.Length + 4];
            Array.Copy(salt, initial, salt.Length);
            initial[^1] = 1;

            var saltedPassword = new byte[HashSize];
            buffer = Hmac(Password, initial);
            Array.Copy(buffer, saltedPassword, buffer.Length);

            for (int i = 1; i < iterations; ++i)
            {
                buffer = Hmac(Password, buffer);
                for (int j = 0; j < buffer.Length; ++j)
                {
                    saltedPassword[j] ^= buffer[j];
                }
            }

            var clientKey = Hmac(saltedPassword, "Client Key".EncodeUtf8());
            var serverKey = Hmac(saltedPassword, "Server Key".EncodeUtf8());
            var storedKey = Hash(clientKey);
            var authMessage = $"{ClientFirstMessageBare},{serverFirstMessage},{sb}".EncodeUtf8();
            var clientSignature = Hmac(storedKey, authMessage);
            ExpectedServerSignature = Convert.ToBase64String(Hmac(serverKey, authMessage));

            // clientKey ^ clientSignature == clientProof, stored in clientSignature to re-use memory
            for (int i = 0; i < HashSize; ++i)
            {
                clientSignature[i] ^= clientKey[i];
            }

            sb.Append(",p=");
            sb.Append(Convert.ToBase64String(clientSignature));

            response = sb.ToString().EncodeUtf8();
            return true;
        }
        else if (State == 3)
        {
            var serverResponse = challenge.DecodeUtf8().Split(',');
            response = Array.Empty<byte>();

            for (var i = 0; i < serverResponse.Length; ++i)
            {
                var piece = serverResponse[i];
                var d = piece.Split('=', 2);
                if (d.Length != 2 || d[0].Length != 1 || !Char.IsAsciiLetter(d[0][0]))
                {
                    return false;
                }

                if (i == 0 && d[0] == "v")
                {
                    if (d[1] != ExpectedServerSignature)
                    {
                        return false;
                    }
                }
                else if (i == 0 && d[0] == "e")
                {
                    // got an error
                    return false;
                }
                else if (i > 0 && !recognized.Contains(d[0][0]))
                {
                    // optional extensions, ignore
                    continue;
                }
                else
                {
                    // invalid/ill-formed response
                    return false;
                }
            }

            return true;
        }

        // invalid state
        response = Array.Empty<byte>();
        return false;
    }

    bool ISaslMechanism.SetChannelBindingData(ChannelBinding? uniqueData, ChannelBinding? endpointData)
    {
        UniqueBindingData = uniqueData;
        EndpointBindingData = endpointData;

        // Binding data is required if we are using a PLUS mechanism,
        // otherwise it is advisory to alert the server whether or not we support binding
        return !Plus || uniqueData != null || endpointData != null;
    }
}
