////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

#pragma warning disable CS0649

namespace FlashCap.Internal;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods_VideoForWindows
{
    public const int WS_CHILD = 0x40000000;
    public const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const int WS_POPUPWINDOW = unchecked ((int)0x80880000);
    public const int WS_VISIBLE = 0x10000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;

    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    private const int SW_HIDE = 0;
    private const int SW_SHOWNORMAL = 1;

    [DllImport("user32", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessage(
        IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(
        IntPtr hWnd, int nCmdShow);
    [DllImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32")]
    public static extern int GetWindowLong(
        IntPtr hWnd, int nIndex);

    [DllImport("user32")]
    public static extern int SetWindowLong(
        IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WaitMessage();

    ////////////////////////////////////////////////////////////////////////

    [DllImport("avicap32", EntryPoint = "capCreateCaptureWindowW", CharSet = CharSet.Unicode)]
    public static extern IntPtr capCreateCaptureWindow(
       string lpszWindowName, int dwStyle,
       int x, int y, int nWidth, int nHeight,
       IntPtr hWndParent, int nID);

    public const int MaxVideoForWindowsDevices = 10;

    [DllImport("avicap32", EntryPoint = "capGetDriverDescriptionW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool capGetDriverDescription(
        uint wDriverIndex,
        StringBuilder lpszName,
        int cbName,
        StringBuilder lpszVer,
        int cbVer);

    ////////////////////////////////////////////////////////////////////////

    private const int WM_CAP_START = 0x400;
    private const int WM_CAP_DRIVER_CONNECT = WM_CAP_START + 10;
    private const int WM_CAP_DRIVER_DISCONNECT = WM_CAP_START + 11;
    private const int WM_CAP_SET_SCALE = WM_CAP_START + 53;
    private const int WM_CAP_SET_PREVIEW = WM_CAP_START + 50;
    private const int WM_CAP_SET_OVERLAY = WM_CAP_START + 51;
    private const int WM_CAP_SET_PREVIEWRATE = WM_CAP_START + 52;
    private const int WM_CAP_GRAB_FRAME_NOSTOP = WM_CAP_START + 61;
    private const int WM_CAP_SET_CALLBACK_FRAME = WM_CAP_START + 5;
    private const int WM_CAP_GET_VIDEOFORMAT = WM_CAP_START + 44;
    private const int WM_CAP_SET_VIDEOFORMAT = WM_CAP_START + 45;
    private const int WM_CAP_DLG_VIDEOFORMAT = WM_CAP_START + 41;
    private const int WM_CAP_DLG_VIDEOSOURCE = WM_CAP_START + 42;
    private const int WM_CAP_DLG_VIDEODISPLAY = WM_CAP_START + 43;
    private const int WM_CAP_DLG_VIDEOCOMPRESSION = WM_CAP_START + 46;
    private const int WM_CAP_GET_SEQUENCE_SETUP = WM_CAP_START + 65;
    private const int WM_CAP_SET_SEQUENCE_SETUP = WM_CAP_START + 64;

    [StructLayout(LayoutKind.Sequential)]
    public struct CAPTUREPARMS
    {
        public int dwRequestMicroSecPerFrame;
        public int fMakeUserHitOKToCapture;
        public int wPercentDropForError;
        public int fYield;
        public int dwIndexSize;
        public int wChunkGranularity;
        public int fCaptureAudio;
        public int wNumVideoRequested;
        public int wNumAudioRequested;
        public int fAbortLeftMouse;
        public int fAbortRightMouse;
        public int fMCIControl;
        public int fStepMCIDevice;
        public int dwMCIStartTime;
        public int dwMCIStopTime;
        public int fStepCaptureAt2x;
        public int wStepCaptureAverageFrames;
        public int dwAudioBufferSize;
    }

    [Flags]
    public enum VideoStatus
    {
        Done = 1,
        Prepared = 2,
        Inqueue = 4,
        KeyFrame = 8,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VIDEOHDR
    {
        public IntPtr lpData;
        public int dwBufferLength;
        public int dwBytesUsed;
        public uint dwTimeCaptured;
        public UIntPtr dwUser;
        public VideoStatus flags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=4)]
        private UIntPtr[] dwReserved;
    }

    public delegate void CAPVIDEOCALLBACK(IntPtr hWnd, in VIDEOHDR vhdr);

    ////////////////////////////////////////////////////////////////////////

    public static void capDriverConnect(IntPtr hWnd, int nDevice) =>
        SendMessage(hWnd, WM_CAP_DRIVER_CONNECT, (IntPtr)nDevice, IntPtr.Zero);
    public static void capDriverDisconnect(IntPtr hWnd, int nDevice) =>
        SendMessage(hWnd, WM_CAP_DRIVER_DISCONNECT, (IntPtr)nDevice, IntPtr.Zero);

    public static void capSetOverlay(IntPtr hWnd, bool enable) =>
        SendMessage(hWnd, WM_CAP_SET_OVERLAY, (IntPtr)(enable ? 1 : 0), IntPtr.Zero);

    public static void capShowPreview(IntPtr hWnd, bool isShow)
    {
        SendMessage(hWnd, WM_CAP_SET_PREVIEW, (IntPtr)(isShow ? 1 : 0), IntPtr.Zero);
        ShowWindow(hWnd, isShow ? SW_SHOWNORMAL : SW_HIDE);
    }

    public static void capGetVideoFormat(
        IntPtr hWnd, out IntPtr pBih)
    {
        var size = SendMessage(hWnd, WM_CAP_GET_VIDEOFORMAT, IntPtr.Zero, IntPtr.Zero);
        pBih = NativeMethods.AllocateMemory(size);
        SendMessage(hWnd, WM_CAP_GET_VIDEOFORMAT, size, pBih);
    }

    public static unsafe bool capSetVideoFormat(
        IntPtr hWnd, IntPtr pBih)
    {
        var pRawBih = (NativeMethods.BITMAPINFOHEADER*)pBih.ToPointer();
        var result = SendMessage(
            hWnd,
            WM_CAP_SET_VIDEOFORMAT,
            (IntPtr)pRawBih->CalculateRawSize(),
            (IntPtr)pRawBih);
        return result != IntPtr.Zero;
    }

    public static unsafe void capCaptureGetSetup(IntPtr hWnd, out CAPTUREPARMS cp)
    {
        fixed (CAPTUREPARMS* p = &cp)
        {
            SendMessage(
                hWnd, WM_CAP_GET_SEQUENCE_SETUP, 
                (IntPtr)sizeof(CAPTUREPARMS), (IntPtr)p);
        }
    }

    public static unsafe bool capCaptureSetSetup(IntPtr hWnd, in CAPTUREPARMS cp)
    {
        fixed (CAPTUREPARMS* p = &cp)
        {
            var result = SendMessage(
                hWnd, WM_CAP_SET_SEQUENCE_SETUP,
                (IntPtr)sizeof(CAPTUREPARMS), (IntPtr)p);
            return result != IntPtr.Zero;
        }
    }

    public static void capSetPreviewScale(IntPtr hWnd, bool scaled) =>
        SendMessage(hWnd, WM_CAP_SET_SCALE, (IntPtr)(scaled ? 1 : 0), IntPtr.Zero);

    public static void capSetPreviewFPS(IntPtr hWnd, int framesPerSecond) =>
        SendMessage(hWnd, WM_CAP_SET_PREVIEWRATE, (IntPtr)framesPerSecond, IntPtr.Zero);

    public static void capGrabFrame(IntPtr hWnd)
    {
        SendMessage(hWnd, WM_CAP_GRAB_FRAME_NOSTOP, IntPtr.Zero, IntPtr.Zero);
    }

    public static void capSetCallbackFrame(IntPtr hWnd, CAPVIDEOCALLBACK? callback)
    {
        var fp = callback is { } ?
            Marshal.GetFunctionPointerForDelegate(callback) : IntPtr.Zero;
        if (SendMessage(hWnd, WM_CAP_SET_CALLBACK_FRAME, IntPtr.Zero, fp) == IntPtr.Zero)
        {
            throw new ArgumentException();
        }
    }

    public static void capGrabFrameNonStop(IntPtr hWnd) =>
        SendMessage(hWnd, WM_CAP_GRAB_FRAME_NOSTOP, IntPtr.Zero, IntPtr.Zero);

    public static void capDlgVideoFormat(IntPtr hWnd) =>
        SendMessage(hWnd, WM_CAP_DLG_VIDEOFORMAT, IntPtr.Zero, IntPtr.Zero);
    public static void capDlgVideoSource(IntPtr hWnd) =>
        SendMessage(hWnd, WM_CAP_DLG_VIDEOSOURCE, IntPtr.Zero, IntPtr.Zero);
    public static void capDlgVideoDisplay(IntPtr hWnd) =>
        SendMessage(hWnd, WM_CAP_DLG_VIDEODISPLAY, IntPtr.Zero, IntPtr.Zero);
    public static void capDlgVideoCompression(IntPtr hWnd) =>
        SendMessage(hWnd, WM_CAP_DLG_VIDEOCOMPRESSION, IntPtr.Zero, IntPtr.Zero);

    [Flags]
    public enum LayeredWindowFlags
    {
        LWA_ALPHA = 0x00000002,
        LWA_COLORKEY = 0x00000001,
    }

    [DllImport("user32")]
    private static extern bool SetLayeredWindowAttributes(
        IntPtr hwnd, uint crKey, byte bAlpha, LayeredWindowFlags dwFlags);

    public static IntPtr CreateVideoSourceWindow(int index)
    {
        // HACK: VFW couldn't operate without any attached window resources.
        //   * It's hider for moving outsite of desktop.
        //   * And will make up transparent tool window with tiny opaque value.
        var handle = capCreateCaptureWindow(
            $"FlashCap_{index}", WS_POPUPWINDOW,
            0, 0, 100, 100, IntPtr.Zero, 0);
        if (handle == IntPtr.Zero)
        {
            var code = Marshal.GetLastWin32Error();
            Marshal.ThrowExceptionForHR(code);
        }

        var extyles = GetWindowLong(
            handle, GWL_EXSTYLE);
        SetWindowLong(
            handle, GWL_EXSTYLE, extyles | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
        SetLayeredWindowAttributes(
            handle, 0, 1, LayeredWindowFlags.LWA_ALPHA);

        return handle;
    }
}
