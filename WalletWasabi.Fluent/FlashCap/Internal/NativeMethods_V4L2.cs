////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlashCap.Internal.V4L2;
using FlashCap.Utilities;

using static FlashCap.Internal.V4L2.NativeMethods_V4L2_Interop;

namespace FlashCap.Internal;

internal static class NativeMethods_V4L2
{
    public static readonly NativeMethods_V4L2_Interop Interop;

    private static readonly Dictionary<uint, PixelFormats> pixelFormats = new();

    static NativeMethods_V4L2()
    {
        utsname buf;
        while (uname(out buf) != 0)
        {
            var hr = Marshal.GetLastWin32Error();
            if (hr != EINTR)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        switch (buf.machine)
        {
            case "x86_64":
            case "amd64":
                Interop = new NativeMethods_V4L2_Interop_x86_64();
                break;
            case "i686":
            case "i586":
            case "i486":
            case "i386":
                Interop = new NativeMethods_V4L2_Interop_i686();
                break;
            case "aarch64":
                Interop = new NativeMethods_V4L2_Interop_aarch64();
                break;
            case "armv9l":
            case "armv8l":
            case "armv7l":
            case "armv6l":
                Interop = new NativeMethods_V4L2_Interop_armv7l();
                break;
            case "mips":
            case "mipsel":
                Interop = new NativeMethods_V4L2_Interop_mips();
                break;
            default:
                throw new InvalidOperationException(
                    $"FlashCap: Architecture '{buf.machine}' is not supported.");
        }

        pixelFormats.Add((uint)NativeMethods.Compression.BI_RGB, PixelFormats.RGB24);
        pixelFormats.Add((uint)NativeMethods.Compression.BI_JPEG, PixelFormats.JPEG);
        pixelFormats.Add((uint)NativeMethods.Compression.BI_PNG, PixelFormats.PNG);
        pixelFormats.Add((uint)NativeMethods.Compression.D3D_RGB24, PixelFormats.RGB24);
        pixelFormats.Add((uint)NativeMethods.Compression.D3D_RGB32, PixelFormats.RGB32);
        pixelFormats.Add((uint)NativeMethods.Compression.D3D_ARGB32, PixelFormats.ARGB32);
        pixelFormats.Add((uint)NativeMethods.Compression.D3D_RGB565, PixelFormats.RGB16);
        pixelFormats.Add((uint)NativeMethods.Compression.D3D_RGB555, PixelFormats.RGB15);
        pixelFormats.Add((uint)NativeMethods.Compression.RGB2, PixelFormats.RGB24);

        pixelFormats.Add(Interop.V4L2_PIX_FMT_RGB332, PixelFormats.RGB8);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_RGB565X, PixelFormats.RGB15);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_RGB565, PixelFormats.RGB16);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_RGB24, PixelFormats.RGB24);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_XRGB32, PixelFormats.RGB32);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_ABGR32, PixelFormats.ARGB32);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_ARGB, PixelFormats.ARGB32);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_MJPEG, PixelFormats.JPEG);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_JPEG, PixelFormats.JPEG);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_UYVY, PixelFormats.UYVY);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_YUYV, PixelFormats.YUYV);
        pixelFormats.Add(Interop.V4L2_PIX_FMT_YUY2, PixelFormats.YUYV);
    }

    public static bool IsKnownPixelFormat(uint pix_fmt) =>
        pixelFormats.ContainsKey(pix_fmt);

    public const int EINTR = 4;
    public const int EINVAL = 22;

    [StructLayout(LayoutKind.Sequential)]
    public struct timeval
    {
        public IntPtr tv_sec;
        public IntPtr tv_usec;
    }

    [Flags]
    public enum OPENBITS
    {
        O_RDONLY = 0,
        O_WRONLY = 1,
        O_RDWR = 2,
    }

    [DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int open(
        [MarshalAs(UnmanagedType.LPStr)] string pathname, OPENBITS flag);

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int read(
        int fd, byte[] buffer, int length);

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int write(
        int fd, byte[] buffer, int count);

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int close(int fd);

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int pipe(int[] filedes);

    [Flags]
    public enum POLLBITS : short
    {
        POLLIN = 0x01,
        POLLPRI = 0x02,
        POLLOUT = 0x04,
        POLLERR = 0x08,
        POLLHUP = 0x10,
        POLLNVAL = 0x20,
        POLLRDNORM = 0x40,
        POLLRDBAND = 0x80,
        POLLWRNORM = 0x100,
        POLLWRBAND = 0x200,
        POLLMSG = 0x400,
        POLLREMOVE = 0x1000,
        POLLRDHUP = 0x2000,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct pollfd
    {
        public int fd;
        public POLLBITS events;
        public POLLBITS revents;
    }

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int poll(
        [In, Out] pollfd[] fds, int nfds, int timeout);

    [Flags]
    public enum PROT
    {
        NONE = 0,
        READ = 1,
        WRITE = 2,
        EXEC = 4,
    }

    [Flags]
    public enum MAP
    {
        SHARED = 1,
        PRIVATE = 2,
    }

    public static readonly IntPtr MAP_FAILED = (IntPtr)(-1);

    [DllImport("libc", EntryPoint = "mmap", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    private static extern IntPtr mmap3232(
        IntPtr addr, uint length, PROT prot, MAP flags, int fd, int offset);
    [DllImport("libc", EntryPoint = "mmap", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    private static extern IntPtr mmap3264(
        IntPtr addr, uint length, PROT prot, MAP flags, int fd, long offset);
    [DllImport("libc", EntryPoint = "mmap", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    private static extern IntPtr mmap6432(
        IntPtr addr, ulong length, PROT prot, MAP flags, int fd, int offset);
    [DllImport("libc", EntryPoint = "mmap", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    private static extern IntPtr mmap6464(
        IntPtr addr, ulong length, PROT prot, MAP flags, int fd, long offset);

    public static IntPtr mmap(
        IntPtr addr, ulong length, PROT prot, MAP flags, int fd, long offset)
    {
        if (Interop.sizeof_size_t == 4)
        {
            if (Interop.sizeof_off_t == 4)
            {
                return mmap3232(addr, (uint)length, prot, flags, fd, (int)offset);
            }
            else
            {
                return mmap3264(addr, (uint)length, prot, flags, fd, offset);
            }
        }
        else
        {
            if (Interop.sizeof_off_t == 4)
            {
                return mmap6432(addr, length, prot, flags, fd, (int)offset);
            }
            else
            {
                return mmap6464(addr, length, prot, flags, fd, offset);
            }
        }
    }

    [DllImport("libc", EntryPoint = "munmap", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    private static extern int munmap32(
        IntPtr addr, uint length);
    [DllImport("libc", EntryPoint = "munmap", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    private static extern int munmap64(
        IntPtr addr, ulong length);

    public static int munmap(
        IntPtr addr, ulong length)
    {
        if (Interop.sizeof_size_t == 4)
        {
            return munmap32(addr, (uint)length);
        }
        else
        {
            return munmap64(addr, length);
        }
    }

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    private static extern int ioctl(
        int fd, UIntPtr request, IntPtr arg);

    public static int ioctl<T>(int fd, uint request, T arg)
    {
        var handle = GCHandle.Alloc(arg, GCHandleType.Pinned);
        try
        {
            while (true)
            {
                var result = ioctl(fd, (UIntPtr)request, handle.AddrOfPinnedObject());
                if (result < 0 && Marshal.GetLastWin32Error() == EINTR)
                {
                    continue;
                }

                return result;
            }
        }
        finally
        {
            handle.Free();
        }
    }

    private const int _UTSNAME_LENGTH = 65;

    [StructLayout(LayoutKind.Sequential)]
    public struct utsname
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = _UTSNAME_LENGTH)] public string sysname;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = _UTSNAME_LENGTH)] public string nodename;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = _UTSNAME_LENGTH)] public string release;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = _UTSNAME_LENGTH)] public string version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = _UTSNAME_LENGTH)] public string machine;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = _UTSNAME_LENGTH)] public string domainname;
    }

    [DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    public static extern int uname(out utsname buf);
    
    ///////////////////////////////////////////////////////////

    public static VideoCharacteristics? CreateVideoCharacteristics(
        uint pix_fmt,
        int width, int height,
        Fraction framesPerSecond,
        string description,
        bool isDiscrete)
    {
        if (pixelFormats.TryGetValue(pix_fmt, out var pixelFormat))
        {
            return new VideoCharacteristics(
                pixelFormat, width, height,
                framesPerSecond.Reduce(),
                description,
                isDiscrete,
                NativeMethods.GetFourCCString((int)pix_fmt));
        }
        else
        {
            Trace.WriteLine($"FlashCap: Unknown format: pix_fmt={NativeMethods.GetFourCCString((int)pix_fmt)}, [{width},{height}], {framesPerSecond}");
            return null;
        }
    }

    public static uint[] GetPixelFormats(
        PixelFormats pixelFormat)
    {
        switch (pixelFormat)
        {
            case PixelFormats.RGB8:
                return new[] { Interop.V4L2_PIX_FMT_RGB332 };
            case PixelFormats.RGB15:
                return new[] { Interop.V4L2_PIX_FMT_RGB565X, (uint)NativeMethods.Compression.D3D_RGB555 };
            case PixelFormats.RGB16:
                return new[] { Interop.V4L2_PIX_FMT_RGB565, (uint)NativeMethods.Compression.D3D_RGB565 };
            case PixelFormats.RGB24:
                return new[] { Interop.V4L2_PIX_FMT_RGB24, (uint)NativeMethods.Compression.BI_RGB, (uint)NativeMethods.Compression.RGB2, (uint)NativeMethods.Compression.D3D_RGB24 };
            case PixelFormats.RGB32:
                return new[] { Interop.V4L2_PIX_FMT_XRGB32, (uint)NativeMethods.Compression.D3D_RGB32 };
            case PixelFormats.ARGB32:
                return new[] { Interop.V4L2_PIX_FMT_ARGB32, Interop.V4L2_PIX_FMT_ARGB, (uint)NativeMethods.Compression.D3D_ARGB32 };
            case PixelFormats.UYVY:
                return new[] { Interop.V4L2_PIX_FMT_UYVY };
            case PixelFormats.YUYV:
                return new[] { Interop.V4L2_PIX_FMT_YUYV, Interop.V4L2_PIX_FMT_YUY2 };
            case PixelFormats.JPEG:
                return new[] { Interop.V4L2_PIX_FMT_MJPEG, Interop.V4L2_PIX_FMT_JPEG, (uint)NativeMethods.Compression.BI_JPEG };
            case PixelFormats.PNG:
                return new[] { (uint)NativeMethods.Compression.BI_PNG };
            default:
                return ArrayEx.Empty<uint>();
        }
    }
}
