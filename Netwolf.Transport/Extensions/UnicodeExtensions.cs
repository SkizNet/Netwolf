using System.Text;

namespace Netwolf.Transport.Extensions;

public static class UnicodeExtensions
{
    internal static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    internal static readonly UTF8Encoding Lax = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static byte[] EncodeUtf8(this string source, bool strict = true)
    {
        return (strict ? Strict : Lax).GetBytes(source);
    }

    public static string DecodeUtf8(this ReadOnlySpan<byte> source, bool strict = true)
    {
        return (strict ? Strict : Lax).GetString(source);
    }

    public static string DecodeUtf8(this byte[] source, bool strict = true)
    {
        return (strict ? Strict : Lax).GetString(source);
    }
}
