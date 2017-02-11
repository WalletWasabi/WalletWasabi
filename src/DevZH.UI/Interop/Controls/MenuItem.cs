using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuItemEnable")]
        public static extern void MenuItemEnable(IntPtr menuItem);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuItemDisable")]
        public static extern void MenuItemDisable(IntPtr menuItem);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuItemOnClicked")]
        public static extern void MenuItemOnClicked(IntPtr menuItem, MenuItemOnClickedDelegate menuItemOnClicked, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuItemChecked")]
        public static extern bool MenuItemChecked(IntPtr menuItem);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMenuItemSetChecked")]
        public static extern void MenuItemSetChecked(IntPtr menuItem, bool isChecked);
    }
}
