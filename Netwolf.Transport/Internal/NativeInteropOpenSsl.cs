using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Internal
{
    internal static partial class NativeInteropOpenSsl
    {
        internal const int SUCCESS = 1;

        [LibraryImport("libssl.so.3", EntryPoint = "SSL_export_keying_material")]
        internal static unsafe partial int ExportKeyingMaterial(SafeHandle sslContext, nint outBuffer, nuint outLen, byte* label, nuint labelLen, nint context, nuint contextLen, int useContext);
    }
}
