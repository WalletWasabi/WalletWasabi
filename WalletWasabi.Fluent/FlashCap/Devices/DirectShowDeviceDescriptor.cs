////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.Threading;
using System.Threading.Tasks;

namespace FlashCap.Devices;

public sealed class DirectShowDeviceDescriptor : CaptureDeviceDescriptor
{
    private readonly string devicePath;

    internal DirectShowDeviceDescriptor(
        string devicePath, string name, string description,
        VideoCharacteristics[] characteristics) :
        base(name, description, characteristics) =>
        this.devicePath = devicePath;

    public override object Identity =>
        this.devicePath;

    public override DeviceTypes DeviceType =>
        DeviceTypes.DirectShow;

    protected override Task<CaptureDevice> OnOpenWithFrameProcessorAsync(
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        FrameProcessor frameProcessor,
        CancellationToken ct) =>
        this.InternalOnOpenWithFrameProcessorAsync(
            new DirectShowDevice(this.devicePath, this.Name),
            characteristics, transcodeIfYUV, frameProcessor, ct);
}
