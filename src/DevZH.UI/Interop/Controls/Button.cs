using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiButtonText")]
        public static extern IntPtr ButtonText(IntPtr button);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiButtonSetText")]
        public static extern void ButtonSetText(IntPtr button, byte[] text);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiButtonOnClicked")]
        public static extern void ButtonOnClicked(IntPtr button, ButtonOnClickedDelegate buttonOnClicked, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewButton")]
        public static extern IntPtr NewButton(byte[] text);
    }
}
