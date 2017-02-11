using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiLabelText")]
        public static extern IntPtr LabelText(IntPtr label);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiLabelSetText")]
        public static extern void LabelSetText(IntPtr label, byte[] text);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewLabel")]
        public static extern IntPtr NewLabel(byte[] text);
    }
}
