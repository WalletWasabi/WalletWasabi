using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowTitle")]
        public static extern IntPtr WindowTitle(IntPtr window);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowSetTitle")]
        public static extern void WindowSetTitle(IntPtr window, byte[] title);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowPosition")]
        public static extern void WindowPosition(IntPtr window, out int x, out int y);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowSetPosition")]
        public static extern void WindowSetPosition(IntPtr window, int x, int y);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowCenter")]
        public static extern void WindowCenter(IntPtr window);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowOnPositionChanged")]
        public static extern void WindowOnPositionChanged(IntPtr window, WindowOnPositionChangedDelegate onPositionChanged, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowContentSize")]
        public static extern void WindowContentSize(IntPtr window, out int width, out int height);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowSetContentSize")]
        public static extern void WindowSetContentSize(IntPtr window, int width, int height);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowFullscreen")]
        public static extern bool WindowFullscreen(IntPtr window);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowSetFullscreen")]
        public static extern void WindowSetFullscreen(IntPtr window, bool fullscreen);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowOnContentSizeChanged")]
        public static extern void WindowOnContentSizeChanged(IntPtr window, WindowOnContentSizeChangedDelegate onContentSizeChanged, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowOnClosing")]
        public static extern void WindowOnClosing(IntPtr window, WindowOnClosingDelegate onClosing, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowBorderless")]
        public static extern bool WindowBorderless(IntPtr window);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowSetBorderless")]
        public static extern void WindowSetBorderless(IntPtr window, bool borderless);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowSetChild")]
        public static extern void WindowSetChild(IntPtr window, IntPtr child);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowMargined")]
        public static extern bool WindowMargined(IntPtr window);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiWindowSetMargined")]
        public static extern void WindowSetMargined(IntPtr window, bool margined);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewWindow")]
        public static extern IntPtr NewWindow(byte[] title, int width, int height, bool hasMenubar);
    }
}
