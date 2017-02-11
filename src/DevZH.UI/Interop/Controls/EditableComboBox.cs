using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiEditableComboboxAppend")]
        public static extern void EditableComboBoxAppend(IntPtr comboBox, byte[] text);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiEditableComboboxText")]
        public static extern IntPtr EditableComboBoxText(IntPtr comboBox);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiEditableComboboxSetText")]
        public static extern void EditableComboBoxSetText(IntPtr comboBox, byte[] text);
        // TODO what do we call a function that sets the currently selected item and fills the text field with it? editable comboboxes have no consistent concept of selected item

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiEditableComboboxOnChanged")]
        public static extern void EditableComboBoxOnChanged(IntPtr comboBox, EditableComboBoxOnChangedDelegate editableComboBoxOnChanged, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewEditableCombobox")]
        public static extern IntPtr NewEditableComboBox();
    }
}
