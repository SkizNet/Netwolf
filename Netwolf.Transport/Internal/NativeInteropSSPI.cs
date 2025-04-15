using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

// compiler complains that arrays as attribute values aren't CLS compliant despite the class being internal
#pragma warning disable CS3016

namespace Netwolf.Transport.Internal
{
    internal static partial class NativeInteropSSPI
    {
        internal const int SEC_E_OK = 0;
        internal const int SECPKG_ATTR_KEYING_MATERIAL_INFO = 0x6a;
        internal const int SECPKG_ATTR_KEYING_MATERIAL = 0x6b;

        [StructLayout(LayoutKind.Sequential)]
        internal struct SecPkgContext_KeyingMaterial
        {
            internal uint cbKeyingMaterial;
            internal nint pbKeyingMaterial;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct SecPkgContext_KeyingMaterialInfo
        {
            internal ushort cbLabel;
            internal byte* pszLabel;
            internal ushort cbContextValue;
            internal nint pbContextValue;
            internal uint cbKeyingMaterial;
        }

        [LibraryImport("Secur32", EntryPoint = "QueryContextAttributes")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial int QueryContextAttributes(SafeHandle phContext, uint ulAttribute, ref SecPkgContext_KeyingMaterial pBuffer);

        [LibraryImport("Secur32", EntryPoint = "SetContextAttributesW")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial int SetContextAttributes(SafeHandle phContext, uint ulAttribute, SecPkgContext_KeyingMaterialInfo pBuffer, uint cbBuffer);

        [LibraryImport("Secur32", EntryPoint = "FreeContextBuffer")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
        internal static partial int FreeContextBuffer(nint pvContextBuffer);
    }
}
