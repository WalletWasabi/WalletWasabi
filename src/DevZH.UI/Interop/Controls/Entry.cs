using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiEntryText")]
        public static extern IntPtr EntryText(IntPtr entry);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiEntrySetText")]
        public static extern void EntrySetText(IntPtr entry, byte[] text);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiEntryOnChanged")]
        public static extern void EntryOnChanged(IntPtr entry, EntryOnChangedDelegate entryOnChanged, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiEntryReadOnly")]
        public static extern bool EntryReadOnly(IntPtr entry);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiEntrySetReadOnly")]
        public static extern void EntrySetReadOnly(IntPtr entry, bool isReadOnly);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewEntry")]
        public static extern IntPtr NewEntry();

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewPasswordEntry")]
        public static extern IntPtr NewPasswordEntry();

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewSearchEntry")]
        public static extern IntPtr NewSearchEntry();
    }
}
