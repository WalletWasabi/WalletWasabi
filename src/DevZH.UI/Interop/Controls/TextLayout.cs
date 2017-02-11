using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        // TODO initial line spacing? and what about leading?
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawNewTextLayout")]
        public static extern IntPtr DrawNewTextLayout(byte[] text, IntPtr defaultFont, double width);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawFreeTextLayout")]
        public static extern void DrawFreeTextLayout(IntPtr layout);

        // TODO get width
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawTextLayoutSetWidth")]
        public static extern void DrawTextLayoutSetWidth(IntPtr layout, double width);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawTextLayoutExtents")]
        public static extern void DrawTextLayoutExtents(IntPtr layout, out double width, out double height);

        // and the attributes that you can set on a text layout

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiDrawTextLayoutSetColor")]
        public static extern void DrawTextLayoutSetColor(IntPtr layout, int startChar, int endChar, double r, double g, double b, double a);

    }
}
