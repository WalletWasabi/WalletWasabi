////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using FlashCap.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FlashCap.Devices;

public sealed class VideoForWindowsDevices : CaptureDevices
{
    protected override IEnumerable<CaptureDeviceDescriptor> OnEnumerateDescriptors() =>
        Enumerable.Range(0, NativeMethods_VideoForWindows.MaxVideoForWindowsDevices).
        Collect(index =>
        {
            var name = new StringBuilder(256);
            var description = new StringBuilder(256);

            if (NativeMethods_VideoForWindows.capGetDriverDescription(
                (uint)index, name, name.Length, description, description.Length))
            {
                var n = name.ToString().Trim();
                var d = description.ToString().Trim();

                return (CaptureDeviceDescriptor)new VideoForWindowsDeviceDescriptor(
                    index,
                    string.IsNullOrEmpty(n) ? "Default" : n,
                    string.IsNullOrEmpty(d) ? "VideoForWindows default" : d,
                    new[] {
                        // DIRTY: VFW couldn't enumerate device specific strictly video formats.
                        //   So there're predefined (major?) formats.
                        NativeMethods.CreateVideoCharacteristics(
                            NativeMethods.Compression.MJPG, 1920, 1080, 0, 30, false)!,
                        NativeMethods.CreateVideoCharacteristics(
                            NativeMethods.Compression.MJPG, 1600, 1200, 0, 30, false)!,
                        NativeMethods.CreateVideoCharacteristics(
                            NativeMethods.Compression.MJPG, 1280, 960, 0, 30, false)!,
                        NativeMethods.CreateVideoCharacteristics(
                            NativeMethods.Compression.MJPG, 1024, 768, 0, 30, false)!,
                        NativeMethods.CreateVideoCharacteristics(
                            NativeMethods.Compression.MJPG, 640, 480, 0, 30, false)!,
                        NativeMethods.CreateVideoCharacteristics(
                            NativeMethods.Compression.MJPG, 640, 480, 0, 15, false)!,
                        NativeMethods.CreateVideoCharacteristics(
                            NativeMethods.Compression.YUYV, 640, 480, 16, 30, false)!,
                        NativeMethods.CreateVideoCharacteristics(
                            NativeMethods.Compression.YUYV, 640, 480, 16, 15, false)!,
                    });
            }
            else
            {
                return null;
            }
        });
}
