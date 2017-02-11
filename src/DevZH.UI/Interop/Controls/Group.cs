using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiGroupTitle")]
        public static extern IntPtr GroupTitle(IntPtr group);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiGroupSetTitle")]
        public static extern void GroupSetTitle(IntPtr group, byte[] title);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiGroupSetChild")]
        public static extern void GroupSetChild(IntPtr group, IntPtr child);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiGroupMargined")]
        public static extern bool GroupMargined(IntPtr group);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiGroupSetMargined")]
        public static extern void GroupSetMargined(IntPtr group, bool margined);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewGroup")]
        public static extern IntPtr NewGroup(byte[] title);
    }
}
