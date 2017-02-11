using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuAppendItem")]
        public static extern IntPtr MenuAppendItem(IntPtr menu, byte[] name);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuAppendCheckItem")]
        public static extern IntPtr MenuAppendCheckItem(IntPtr menu, byte[] name);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuAppendQuitItem")]
        public static extern IntPtr MenuAppendQuitItem(IntPtr menu);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuAppendPreferencesItem")]
        public static extern IntPtr MenuAppendPreferencesItem(IntPtr menu);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuAppendAboutItem")]
        public static extern IntPtr MenuAppendAboutItem(IntPtr menu);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuAppendSeparator")]
        public static extern void MenuAppendSeparator(IntPtr menu);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewMenu")]
        public static extern IntPtr NewMenu(byte[] name);
    }
}
