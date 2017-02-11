using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiBoxAppend")]
        public static extern void BoxAppend(IntPtr parent, IntPtr child, bool stretchy);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiBoxDelete")]
        public static extern void BoxDelete(IntPtr parent, int index);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiBoxPadded")]
        public static extern bool BoxPadded(IntPtr box);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiBoxSetPadded")]
        public static extern void BoxSetPadded(IntPtr box, bool padded);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewHorizontalBox")]
        public static extern IntPtr NewHorizontalBox();

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewVerticalBox")]
        public static extern IntPtr NewVerticalBox();

    }
}
