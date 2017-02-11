using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlDestroy", SetLastError = true)]
        public static extern void ControlDestroy(IntPtr control);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlHandle")]
        public static extern UIntPtr ControlHandle(IntPtr control);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlParent")]
        public static extern IntPtr ControlParent(IntPtr control);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlSetParent")]
        public static extern void ControlSetParent(IntPtr control, IntPtr parent);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlToplevel")]
        public static extern bool ControlToplevel(IntPtr control);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlVisible")]
        public static extern bool ControlVisible(IntPtr control);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlShow")]
        public static extern void ControlShow(IntPtr control);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlHide")]
        public static extern void ControlHide(IntPtr control);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlEnabled")]
        public static extern bool ControlEnabled(IntPtr control);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlEnable")]
        public static extern void ControlEnable(IntPtr control);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlDisable")]
        public static extern void ControlDisable(IntPtr control);

        /* This is unneccesary.
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiAllocControl")]
        public static extern IntPtr AllocControl(UIntPtr size, uint osSignature, uint typeSignature, string typeName);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiFreeControl")]
        public static extern void FreeControl(IntPtr control);

        // TODO make sure all controls have these(belows)
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlVerifySetParent")]
        public static extern void ControlVerifySetParent(IntPtr control, IntPtr parent);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiControlEnabledToUser")]
        public static extern bool ControlEnabledToUser(IntPtr control);
        */
    }
}
