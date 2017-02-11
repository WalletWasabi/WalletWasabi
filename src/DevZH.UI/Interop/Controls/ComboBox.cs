using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiComboboxAppend")]
        public static extern void ComboBoxAppend(IntPtr comboBox, byte[] text);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiComboboxSelected")]
        public static extern int ComboBoxSelected(IntPtr comboBox);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiComboboxSetSelected")]
        public static extern void ComboBoxSetSelected(IntPtr comboBox, int n);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiComboboxOnSelected")]
        public static extern void ComboBoxOnSelected(IntPtr comboBox, ComboBoxOnSelectedDelegate comboBoxOnSelected, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewCombobox")]
        public static extern IntPtr NewComboBox();
    }
}
