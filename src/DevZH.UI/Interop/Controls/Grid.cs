using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiGridAppend")]
        public static extern void GridAppend(IntPtr grid, IntPtr child, int left, int top, int xspan, int yspan, int hexpand, HorizontalAlignment halign, int vexpand, VerticalAlignment valign);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiGridInsertAt")]
        public static extern void GridInsertAt(IntPtr grid, IntPtr child, IntPtr existing, GridEdge at, int xspan, int yspan, int hexpand, HorizontalAlignment halign, int vexpand, VerticalAlignment valign);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiGridPadded")]
        public static extern bool GridPadded(IntPtr grid);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiGridSetPadded")]
        public static extern void GridSetPadded(IntPtr grid, bool padded);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewGrid")]
        public static extern IntPtr NewGrid();
    }
}
