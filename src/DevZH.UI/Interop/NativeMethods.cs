using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal static partial class NativeMethods
    {
        // ReSharper disable once InconsistentNaming
        private const string LibUI = "libui";

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiInit")]
        public static extern string Init(ref InitOptions options);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiUninit")]
        public static extern void UnInit();

        // It can use string directly, 'cause these are printed as ansi by libui
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiFreeInitError")]
        public static extern void FreeInitError(string error);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMain")]
        public static extern void Main();

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMainSteps")]
        public static extern void MainSteps();

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiMainStep")]
        public static extern bool MainStep(bool wait);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiQuit")]
        public static extern void Quit();

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiFreeText")]
        public static extern void FreeText(IntPtr text);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiQueueMain")]
        public static extern void QueueMain(QueueMainDelegate f, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiOnShouldQuit")]
        public static extern void OnShouldQuit(OnShouldQuitDelegate f, IntPtr data);
    }
}
