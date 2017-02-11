using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void QueueMainDelegate(IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool OnShouldQuitDelegate(IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void WindowOnPositionChangedDelegate(IntPtr window, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void WindowOnContentSizeChangedDelegate(IntPtr window, IntPtr data);

        // TODO: bool or int(enum)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool WindowOnClosingDelegate(IntPtr window, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ButtonOnClickedDelegate(IntPtr button, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CheckBoxOnToggledDelegate(IntPtr checkbox, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void EntryOnChangedDelegate(IntPtr entry, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SpinBoxOnChangedDelegate(IntPtr spinBox, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SliderOnChangedDelegate(IntPtr slider, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ComboBoxOnSelectedDelegate(IntPtr comboBox, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void EditableComboBoxOnChangedDelegate(IntPtr comboBox, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void RadioButtonOnSelectedDelegate(IntPtr radioButton, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MultilineEntryOnChangedDelegate(IntPtr entry, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MenuItemOnClickedDelegate(IntPtr menuItem, IntPtr window, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ColorButtonOnChangedDelegate(IntPtr button, IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FontButtonOnChangedDelegate(IntPtr button, IntPtr data);
    }
}
