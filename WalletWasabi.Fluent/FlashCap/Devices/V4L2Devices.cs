////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using FlashCap.Internal;
using FlashCap.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using static FlashCap.Internal.NativeMethods_V4L2;
using static FlashCap.Internal.V4L2.NativeMethods_V4L2_Interop;

namespace FlashCap.Devices;

public sealed class V4L2Devices : CaptureDevices
{
    private static IEnumerable<v4l2_fmtdesc> EnumerateFormatDesc(
        int fd) =>
        Enumerable.Range(0, 1000).
        CollectWhile(index =>
        {
            var fmtdesc = Interop.Create_v4l2_fmtdesc();
            fmtdesc.index = (uint)index;
            fmtdesc.type = (uint)v4l2_buf_type.VIDEO_CAPTURE;
            
            return
                ioctl(fd, Interop.VIDIOC_ENUM_FMT, fmtdesc) == 0 &&
                IsKnownPixelFormat(fmtdesc.pixelformat) ?
                (v4l2_fmtdesc?)fmtdesc : null;
        }).
        ToArray();   // Important: Iteration process must be continuous, avoid ioctl calls with other requests.

    private struct FrameSize
    {
        public int Width;
        public int Height;
        public bool IsDiscrete;
    }

    private static IEnumerable<FrameSize> EnumerateFrameSize(
        int fd, uint pixelFormat) =>
        Enumerable.Range(0, 1000).
        CollectWhile(index =>
        {
            var frmsizeenum = Interop.Create_v4l2_frmsizeenum();
            frmsizeenum.index = (uint)index;
            frmsizeenum.pixel_format = pixelFormat;

            return ioctl(fd, Interop.VIDIOC_ENUM_FRAMESIZES, frmsizeenum) == 0 ?
                (v4l2_frmsizeenum?)frmsizeenum : null;
        }).
        // Expand when both stepwise and continuous:
        SelectMany(frmsizeenum =>
        {
            static IEnumerable<FrameSize> EnumerateStepWise(
                v4l2_frmsize_stepwise stepwise) =>
                NativeMethods.DefactoStandardResolutions.
                    Where(r =>
                        r.Width >= stepwise.min_width &&
                        r.Width <= stepwise.max_height &&
                        (r.Width - stepwise.min_width % stepwise.step_width) == 0 &&
                        r.Height >= stepwise.min_height &&
                        r.Height <= stepwise.max_height &&
                        (r.Height - stepwise.min_height % stepwise.step_height) == 0).
                    OrderByDescending(r => r).
                    Select(r => new FrameSize
                        { Width = r.Width, Height = r.Height, IsDiscrete = false, });

            static IEnumerable<FrameSize> EnumerateContinuous(
                v4l2_frmsize_stepwise stepwise) =>
                NativeMethods.DefactoStandardResolutions.
                    Where(r =>
                        r.Width >= stepwise.min_width &&
                        r.Width <= stepwise.max_height &&
                        r.Height >= stepwise.min_height &&
                        r.Height <= stepwise.max_height).
                    OrderByDescending(r => r).
                    Select(r => new FrameSize
                        { Width = r.Width, Height = r.Height, IsDiscrete = false, });

            return (v4l2_frmsizetypes)frmsizeenum.type switch
            {
                v4l2_frmsizetypes.DISCRETE =>
                    new[] { new FrameSize
                        { Width = (int)frmsizeenum.discrete.width, Height = (int)frmsizeenum.discrete.height, IsDiscrete = true, }, },
                v4l2_frmsizetypes.STEPWISE =>
                    EnumerateStepWise(frmsizeenum.stepwise),
                _ =>
                    EnumerateContinuous(frmsizeenum.stepwise),
            };
        }).
        ToArray();   // Important: Iteration process must be continuous, avoid ioctl calls with other requests.

    private struct FramesPerSecond
    {
        public Fraction Value;
        public bool IsDiscrete;
    }

    private static IEnumerable<FramesPerSecond> EnumerateFramesPerSecond(
        int fd, uint pixelFormat, int width, int height) =>
        Enumerable.Range(0, 1000).
        CollectWhile(index =>
        {
            var frmivalenum = Interop.Create_v4l2_frmivalenum();
            frmivalenum.index = (uint)index;
            frmivalenum.pixel_format = pixelFormat;
            frmivalenum.width = (uint)width;
            frmivalenum.height = (uint)height;

            return ioctl(fd, Interop.VIDIOC_ENUM_FRAMEINTERVALS, frmivalenum) == 0 ?
                (v4l2_frmivalenum?)frmivalenum : null;
        }).
        SelectMany(frmivalenum =>
        {
            // v4l2_fract is "interval", so makes fps to do reciprocal.
            // (numerator <--> denominator)
            static IEnumerable<FramesPerSecond> EnumerateStepWise(
                v4l2_frmival_stepwise stepwise)
            {
                var min = new Fraction((int)stepwise.min.denominator, (int)stepwise.min.numerator);
                var max = new Fraction((int)stepwise.max.denominator, (int)stepwise.max.numerator);
                var step = new Fraction((int)stepwise.step.denominator, (int)stepwise.step.numerator);
                return NativeMethods.DefactoStandardFramesPerSecond.
                    Where(fps =>
                        fps >= min && fps <= max &&
                        ((fps - min) % step) == 0).
                    OrderByDescending(fps => fps).
                    Select(fps => new FramesPerSecond { Value = fps, IsDiscrete = false, });
            }

            static IEnumerable<FramesPerSecond> EnumerateContinuous(
                v4l2_frmival_stepwise stepwise)
            {
                var min = new Fraction((int)stepwise.min.denominator, (int)stepwise.min.numerator);
                var max = new Fraction((int)stepwise.max.denominator, (int)stepwise.max.numerator);
                return NativeMethods.DefactoStandardFramesPerSecond.
                    Where(fps => fps >= min && fps <= max).
                    OrderByDescending(fps => fps).
                    Select(fps => new FramesPerSecond { Value = fps, IsDiscrete = false, });
            }

            return (v4l2_frmivaltypes)frmivalenum.type switch
            {
                v4l2_frmivaltypes.DISCRETE =>
                    new [] { new FramesPerSecond
                        { Value = new Fraction((int)frmivalenum.discrete.denominator, (int)frmivalenum.discrete.numerator), IsDiscrete = true, }, },
                v4l2_frmivaltypes.STEPWISE =>
                    EnumerateStepWise(frmivalenum.stepwise),
                _ =>
                    EnumerateContinuous(frmivalenum.stepwise),
            };
        }).
        ToArray();   // Important: Iteration process must be continuous, avoid ioctl calls with other requests.

    private static string ToString(byte[] data)
    {
        var count = Array.IndexOf(data, (byte)0);
        var s = Encoding.UTF8.GetString(data);
        var str = Encoding.UTF8.GetString(data, 0, count);
        return str;
    }

    protected override IEnumerable<CaptureDeviceDescriptor> OnEnumerateDescriptors() =>
        Directory.GetFiles("/dev", "video*").
        Collect(devicePath =>
        {
            if (open(devicePath, OPENBITS.O_RDWR) is { } fd && fd >= 0)
            {
                try
                {
                    var caps = Interop.Create_v4l2_capability();
                    if (ioctl(fd, Interop.VIDIOC_QUERYCAP, caps) >= 0 &&
                        (caps.capabilities & Interop.V4L2_CAP_VIDEO_CAPTURE) == Interop.V4L2_CAP_VIDEO_CAPTURE)
                    {
                        return (CaptureDeviceDescriptor)new V4L2DeviceDescriptor(
                            devicePath, ToString(caps.card), $"{ToString(caps.bus_info)}: {ToString(caps.driver)}",
                            EnumerateFormatDesc(fd).
                            SelectMany(fmtdesc =>
                                EnumerateFrameSize(fd, fmtdesc.pixelformat).
                                SelectMany(frmsize =>
                                    EnumerateFramesPerSecond(fd, fmtdesc.pixelformat, frmsize.Width, frmsize.Height).
                                    Collect(framesPerSecond =>
                                        NativeMethods_V4L2.CreateVideoCharacteristics(
                                            fmtdesc.pixelformat, frmsize.Width, frmsize.Height,
                                            framesPerSecond.Value, ToString(fmtdesc.description),
                                            frmsize.IsDiscrete && framesPerSecond.IsDiscrete)))).
                            Distinct().
                            OrderByDescending(vc => vc).
                            ToArray());
                    }
                    else
                    {
                        return null;
                    }
                }
                finally
                {
                    close(fd);
                }
            }
            else
            {
                return null;
            }
        });
}
