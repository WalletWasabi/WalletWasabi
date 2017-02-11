using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMultilineEntryText")]
        public static extern IntPtr MultilineEntryText(IntPtr entry);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMultilineEntrySetText")]
        public static extern void MultilineEntrySetText(IntPtr entry, byte[] text);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMultilineEntryAppend")]
        public static extern void MultilineEntryAppend(IntPtr entry, byte[] text);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMultilineEntryOnChanged")]
        public static extern void MultilineEntryOnChanged(IntPtr entry, MultilineEntryOnChangedDelegate multilineEntryOnChanged, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMultilineEntryReadOnly")]
        public static extern bool MultilineEntryReadOnly(IntPtr entry);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMultilineEntrySetReadOnly")]
        public static extern void MultilineEntrySetReadOnly(IntPtr entry, bool isReadOnly);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewMultilineEntry")]
        public static extern IntPtr NewMultilineEntry();

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewNonWrappingMultilineEntry")]
        public static extern IntPtr NewNonWrappingMultilineEntry();
    }
}
