using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI.Drawing
{
    public class Functions
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        //public delegate void AreaHandlerDraw(ControlHandle handler, ControlHandle area, [In, Out]ref AreaDrawParams param);
        internal delegate void AreaHandlerDraw(IntPtr handler, IntPtr area, [In, Out]ref AreaDrawParamsInternal param);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        // TODO document that resizes cause a full redraw for non-scrolling areas; implementation-defined for scrolling areas
        //public delegate void AreaHandlerMouseEvent(ControlHandle handler, ControlHandle area, [In, Out]ref AreaMouseEvent mouseEvent);
        internal delegate void AreaHandlerMouseEvent(IntPtr handler, IntPtr area, [In, Out]ref AreaMouseEvent mouseEvent);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        // TODO document that on first show if the mouse is already in the uiArea then one gets sent with left=0
        // TODO what about when the area is hidden and then shown again?
        internal delegate void AreaHandlerMouseCrossed(IntPtr handler, IntPtr area, int left);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void AreaHandlerDragBroken(IntPtr handler, IntPtr area);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        //public delegate int AreaHandlerKeyEvent(ControlHandle handler, ControlHandle area, [In, Out]ref AreaKeyEvent keyEvent);
        internal delegate bool AreaHandlerKeyEvent(IntPtr handler, IntPtr area, [In, Out]ref AreaKeyEvent keyEvent);
    }
}
