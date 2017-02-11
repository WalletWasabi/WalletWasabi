using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiTabAppend")]
        public static extern void TabAppend(IntPtr tab, byte[] name, IntPtr child);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiTabInsertAt")]
        public static extern void TabInsertAt(IntPtr tab, byte[] name, int before, IntPtr child);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiTabDelete")]
        public static extern void TabDelete(IntPtr tab, int index);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiTabNumPages")]
        public static extern int TabNumPages(IntPtr tab);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiTabMargined")]
        public static extern bool TabMargined(IntPtr tab, int page);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiTabSetMargined")]
        public static extern void TabSetMargined(IntPtr tab, int page, bool margined);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewTab")]
        public static extern IntPtr NewTab();
    }
}
