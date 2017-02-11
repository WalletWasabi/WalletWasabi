using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Drawing;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        // TODO document this returns a new font
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiFontButtonFont")]
        public static extern IntPtr FontButtonFont(IntPtr button);
        
        // TODO SetFont, mechanics
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiFontButtonOnChanged")]
        public static extern void FontButtonOnChanged(IntPtr button, FontButtonOnChangedDelegate fontButtonOnChanged, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewFontButton")]
        public static extern IntPtr NewFontButton();
    }
}
