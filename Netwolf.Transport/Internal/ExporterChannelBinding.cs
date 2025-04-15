using Microsoft.Extensions.Logging;

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
    /// <summary>
    /// The label specified in RFC 9266 for tls-exporter channel binding
    /// </summary>
    /// <remarks>
    /// While the TLS spec expects that the label is not null-terminated, the Windows API expects a null-terminated string
    /// We allocate as a byte[] to avoid unnecessary conversion from the UTF-16 native C# string type
    /// </remarks>
    private static readonly byte[] EXPORTER_LABEL = "EXPORTER-Channel-Binding\x00"u8.ToArray();

    /// <summary>
    ///  Number of bytes we need per RFC 9266 for the channel binding data
    /// </summary>
    private const int EXPORTER_RESULT_LENGTH = 32;

    private Func<nint, bool> ReleaseCallback { get; init; }

    private int Length { get; init; }

    public override int Size => Length;

    /// <summary>
    /// Export keying material (RFC 5705/8446) for the tls-exporter channel binding (RFC 9266)
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    internal unsafe static ExporterChannelBinding? Build(SslStream stream, ILogger logger)
    {
        /* References: https://github.com/dotnet/runtime/tree/v9.0.3/src/libraries/System.Net.Security/src/System/Net/Security
         * SslStream.*.cs, SslStreamPal.*.cs
         */
        if (typeof(SslStream)?.GetField("_securityContext", BindingFlags.NonPublic)?.GetValue(stream) is not SafeHandle context)
        {
            logger.LogWarning("SslStream._securityContext no longer exists or is no longer a SafeHandle");
            return null;
        }

        fixed (byte* label = EXPORTER_LABEL)
        {
            if (OperatingSystem.IsWindows())
            {
                NativeInteropSSPI.SecPkgContext_KeyingMaterialInfo info = new()
                {
                    // Windows API expects length to include the null byte
                    cbLabel = (ushort)EXPORTER_LABEL.Length,
                    pszLabel = label,
                    cbContextValue = 0, // no context is passed
                    pbContextValue = nint.Zero,
                    cbKeyingMaterial = EXPORTER_RESULT_LENGTH
                };

                var result = NativeInteropSSPI.SetContextAttributes(
                    context,
                    NativeInteropSSPI.SECPKG_ATTR_KEYING_MATERIAL_INFO,
                    info,
                    (uint)Marshal.SizeOf<NativeInteropSSPI.SecPkgContext_KeyingMaterialInfo>());

                if (result != NativeInteropSSPI.SEC_E_OK)
                {
                    logger.LogWarning("Unable to set keying material info for tls-exporter: {ResultCode}", result);
                    return null;
                }

                NativeInteropSSPI.SecPkgContext_KeyingMaterial binding = new();

                result = NativeInteropSSPI.QueryContextAttributes(
                    context,
                    NativeInteropSSPI.SECPKG_ATTR_KEYING_MATERIAL,
                    ref binding);

                if (result != NativeInteropSSPI.SEC_E_OK)
                {
                    logger.LogWarning("Unable to query binding info for tls-exporter: {ResultCode}", result);
                    return null;
                }

                if (binding.cbKeyingMaterial != EXPORTER_RESULT_LENGTH)
                {
                    logger.LogWarning("Exported key material is not of the correct length; expected {Expected} and got {Actual}", EXPORTER_RESULT_LENGTH, binding.cbKeyingMaterial);
                    return null;
                }

                return new ExporterChannelBinding(
                    binding.pbKeyingMaterial,
                    (int)binding.cbKeyingMaterial,
                    p => NativeInteropSSPI.FreeContextBuffer(p) == NativeInteropSSPI.SEC_E_OK);
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsWatchOS())
            {
                // Currently unsupported platform; .NET is using an older/deprecated API and we therefore can't call the functions on the more recent API that supports this
                return null;
            }
            else if (OperatingSystem.IsAndroid())
            {
                // Can be supported in theory per SSLEngines.exportKeyingMaterial but would likely need a native thunk built for the JNI interop. P/Invoke just doesn't cut it here.
                return null;
            }
            else if (OperatingSystem.IsBrowser() || OperatingSystem.IsWasi())
            {
                // Unsupported platform. This method is called unconditionally so don't throw here; just indicate this particular binding type isn't supported
                return null;
            }
            else
            {
                // fall back to OpenSSL
                nint buffer = Marshal.AllocHGlobal(EXPORTER_RESULT_LENGTH);
                var result = NativeInteropOpenSsl.ExportKeyingMaterial(context, buffer, EXPORTER_RESULT_LENGTH, label, (uint)EXPORTER_LABEL.Length - 1, nint.Zero, 0, 1);
                if (result != NativeInteropOpenSsl.SUCCESS)
                {
                    // Should see if there's a way to actually extract useful error info from OpenSSL since it returns either 0 or -1 on failure.
                    // SSL_get_error() doesn't document SSL_ExportKeyingMaterial as one of the functions that works with it. Maybe there is something else?
                    logger.LogWarning("Unable to export keying material for tls-exporter: {ResultCode}", result);
                    Marshal.FreeHGlobal(buffer);
                    return null;
                }

                return new ExporterChannelBinding(buffer, EXPORTER_RESULT_LENGTH, p => { Marshal.FreeHGlobal(p); return true; });
            }
        }
        
    }

    private ExporterChannelBinding(nint handleValue, int length, Func<nint, bool> releaseCallback)
    {
        handle = handleValue;
        Length = length;
        ReleaseCallback = releaseCallback;
    }

    /// <summary>
    /// Release resources pointed to by the handle.
    /// This is only called by SafeHandle.cs if the handle isn't invalid (i.e. isn't 0 or -1).
    /// It is also only called once per SafeHandle class instance.
    /// As such, we do not need duplicative checks for invalid handles or to avoid double-frees here
    /// since those will be guaranteed to not happen by our parent.
    /// </summary>
    /// <returns></returns>
    protected override bool ReleaseHandle()
    {
        return ReleaseCallback(handle);
    }
}
