using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Drawing;
using DevZH.UI.Interop;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        // TODO give a better name
        // TODO document the types of width and height
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiAreaSetSize")]
        public static extern void AreaSetSize(IntPtr area, int width, int height);

        // TODO uiAreaQueueRedraw()
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiAreaQueueRedrawAll")]
        public static extern void AreaQueueReDrawAll(IntPtr area);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiAreaScrollTo")]
        public static extern void AreaScrollTo(IntPtr area, double x, double y, double width, double height);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewArea")]
        public static extern IntPtr NewArea( AreaHandlerInternal ah);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewScrollingArea")]
        public static extern IntPtr NewScrollingArea( AreaHandlerInternal ah, int width, int height);
    }
}
