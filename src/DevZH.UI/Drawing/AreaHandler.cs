using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DevZH.UI.Drawing;

namespace DevZH.UI.Drawing
{
    [StructLayout(LayoutKind.Sequential)]
    internal class AreaHandler
    {
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public Functions.AreaHandlerDraw Draw;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public Functions.AreaHandlerMouseEvent MouseEvent;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public Functions.AreaHandlerMouseCrossed MouseCrossed;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public Functions.AreaHandlerDragBroken DragBroken;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public Functions.AreaHandlerKeyEvent KeyEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class AreaHandlerInternal
    {
        public IntPtr Draw;

        public IntPtr MouseEvent;

        public IntPtr MouseCrossed;

        public IntPtr DragBroken;

        public IntPtr KeyEvent;
    }
}
