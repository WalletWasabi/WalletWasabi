using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiOpenFile")]
        public static extern IntPtr OpenFile(IntPtr parent);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiSaveFile")]
        public static extern IntPtr SaveFile(IntPtr parent);
    }
}
