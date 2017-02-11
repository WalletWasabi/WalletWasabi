using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiColorButtonColor")]
        public static extern void ColorButtonColor(IntPtr button, out double red, out double green, out double blue, out double alpha);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiColorButtonSetColor")]
        public static extern void ColorButtonSetColor(IntPtr button, double red, double green, double blue, double alpha);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiColorButtonOnChanged")]
        public static extern void ColorButtonOnChanged(IntPtr button, ColorButtonOnChangedDelegate colorButtonOnChanged, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewColorButton")]
        public static extern IntPtr NewColorButton();
    }
}
