using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMsgBox")]
        public static extern void MsgBox(IntPtr parent, byte[] title, byte[] description);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMsgBoxError")]
        public static extern void MsgBoxError(IntPtr parent, byte[] title, byte[] description);
    }
}
