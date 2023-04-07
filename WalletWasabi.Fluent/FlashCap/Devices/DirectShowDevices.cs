////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using FlashCap.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlashCap.Devices;

public sealed class DirectShowDevices : CaptureDevices
{
    protected override IEnumerable<CaptureDeviceDescriptor> OnEnumerateDescriptors() =>
        NativeMethods_DirectShow.EnumerateDeviceMoniker(
            NativeMethods_DirectShow.CLSID_VideoInputDeviceCategory).
        Collect(moniker => moniker.GetPropertyBag() is { } pb ?
            pb.SafeReleaseBlock(pb =>
                pb.GetValue("FriendlyName", default(string))?.Trim() is { } n &&
                (string.IsNullOrEmpty(n) ? "Unknown" : n!) is { } name &&
                pb.GetValue("DevicePath", default(string))?.Trim() is { } devicePath ?
                    (CaptureDeviceDescriptor)new DirectShowDeviceDescriptor(
                        devicePath, name,
                        pb.GetValue("Description", default(string))?.Trim() ?? $"{name} (DirectShow)",
                        moniker.BindToObject(
                            null, null, in NativeMethods_DirectShow.IID_IBaseFilter, out var cs) == 0 &&
                        cs is NativeMethods_DirectShow.IBaseFilter captureSource ?
                            captureSource.SafeReleaseBlock(
                                captureSource => captureSource.EnumeratePins().
                                Collect(pin =>
                                    pin.GetPinInfo() is { } pinInfo &&
                                    pinInfo.dir == NativeMethods_DirectShow.PIN_DIRECTION.Output ?
                                        pin : null).
                                SelectMany(pin =>
                                    pin.EnumerateFormats().
                                    Collect(format => format.CreateVideoCharacteristics())).
                                Distinct().
                                OrderByDescending(vc => vc).
                                ToArray()) :
                            ArrayEx.Empty<VideoCharacteristics>()) :
                    null) :
            null);
}
