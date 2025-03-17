using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Security;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Internal;

internal class ExporterChannelBinding : ChannelBinding
{
    private int Length { get; set; }

    public override int Size => Length;

    internal unsafe static ExporterChannelBinding? Build(SslStream stream)
    {
        /* References: https://github.com/dotnet/runtime/tree/v9.0.3/src/libraries/System.Net.Security/src/System/Net/Security
         * SslStream.*.cs, SslStreamPal.*.cs
         */
        if (typeof(SslStream)?.GetField("_securityContext", BindingFlags.NonPublic)?.GetValue(stream) is not SafeHandle context)
        {
            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Values per RFC 9266
            NativeInteropSSPI.SecPkgContext_KeyingMaterialInfo info = new()
            {
                cbLabel = 25, // length of "EXPORTER-Channel-Binding", including terminating NULL byte
                pszLabel = "EXPORTER-Channel-Binding",
                cbContextValue = 0, // no context is passed
                pbContextValue = nint.Zero,
                cbKeyingMaterial = 32 // we need 32 bytes back out
            };

            var result = NativeInteropSSPI.SetContextAttributes(
                context,
                NativeInteropSSPI.SECPKG_ATTR_KEYING_MATERIAL_INFO,
                info,
                (uint)Marshal.SizeOf<NativeInteropSSPI.SecPkgContext_KeyingMaterialInfo>());

            NativeInteropSSPI.SecPkgContext_KeyingMaterial binding = new();

            result = NativeInteropSSPI.QueryContextAttributes(
                context,
                NativeInteropSSPI.SECPKG_ATTR_KEYING_MATERIAL,
                ref binding);
        }
    }

    private ExporterChannelBinding(nint handleValue, int length)
    {
        handle = handleValue;
        Length = length;
    }

    protected override bool ReleaseHandle()
    {
        throw new NotImplementedException();
    }
}
