////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using FlashCap.FrameProcessors;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlashCap;

public static class CaptureDeviceDescriptorExtension
{
    public static Task<CaptureDevice> OpenWithFrameProcessorAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        FrameProcessor frameProcessor,
        CancellationToken ct = default) =>
        descriptor.InternalOpenWithFrameProcessorAsync(characteristics, transcodeIfYUV, frameProcessor, ct);

    //////////////////////////////////////////////////////////////////////////////////

    public static Task<CaptureDevice> OpenAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        PixelBufferArrivedDelegate pixelBufferArrived,
        CancellationToken ct = default) =>
        descriptor.OpenWithFrameProcessorAsync(
            characteristics, true,
            new DelegatedQueuingProcessor(pixelBufferArrived, 1),
            ct);

    public static Task<CaptureDevice> OpenAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        PixelBufferArrivedDelegate pixelBufferArrived,
        CancellationToken ct = default) =>
        descriptor.OpenWithFrameProcessorAsync(
            characteristics, transcodeIfYUV,
            new DelegatedQueuingProcessor(pixelBufferArrived, 1),
            ct);

    public static Task<CaptureDevice> OpenAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        bool isScattering,
        int maxQueuingFrames,
        PixelBufferArrivedDelegate pixelBufferArrived,
        CancellationToken ct = default) =>
        descriptor.OpenWithFrameProcessorAsync(
            characteristics, transcodeIfYUV,
            isScattering ?
                new DelegatedScatteringProcessor(pixelBufferArrived, maxQueuingFrames) :
                new DelegatedQueuingProcessor(pixelBufferArrived, maxQueuingFrames),
            ct);

    //////////////////////////////////////////////////////////////////////////////////

    public static Task<CaptureDevice> OpenAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        PixelBufferArrivedTaskDelegate pixelBufferArrived,
        CancellationToken ct = default) =>
        descriptor.OpenWithFrameProcessorAsync(
            characteristics, true,
            new DelegatedQueuingTaskProcessor(pixelBufferArrived, 1),
            ct);

    public static Task<CaptureDevice> OpenAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        PixelBufferArrivedTaskDelegate pixelBufferArrived,
        CancellationToken ct = default) =>
        descriptor.OpenWithFrameProcessorAsync(
            characteristics, transcodeIfYUV,
            new DelegatedQueuingTaskProcessor(pixelBufferArrived, 1),
            ct);

    public static Task<CaptureDevice> OpenAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        bool isScattering,
        int maxQueuingFrames,
        PixelBufferArrivedTaskDelegate pixelBufferArrived,
        CancellationToken ct = default) =>
        descriptor.OpenWithFrameProcessorAsync(
            characteristics, transcodeIfYUV,
            isScattering ?
                new DelegatedScatteringTaskProcessor(pixelBufferArrived, maxQueuingFrames) :
                new DelegatedQueuingTaskProcessor(pixelBufferArrived, maxQueuingFrames),
            ct);

    //////////////////////////////////////////////////////////////////////////////////

    public static async Task<ObservableCaptureDevice> AsObservableAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        CancellationToken ct = default)
    {
        var observerProxy = new ObservableCaptureDevice.ObserverProxy();
        var captureDevice = await descriptor.OpenWithFrameProcessorAsync(
            characteristics, true,
            new DelegatedQueuingProcessor(observerProxy.OnPixelBufferArrived, 1),
            ct).
            ConfigureAwait(false);

        return new ObservableCaptureDevice(captureDevice, observerProxy);
    }

    public static async Task<ObservableCaptureDevice> AsObservableAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        CancellationToken ct = default)
    {
        var observerProxy = new ObservableCaptureDevice.ObserverProxy();
        var captureDevice = await descriptor.OpenWithFrameProcessorAsync(
            characteristics, transcodeIfYUV,
            new DelegatedQueuingProcessor(observerProxy.OnPixelBufferArrived, 1),
            ct).
            ConfigureAwait(false);

        return new ObservableCaptureDevice(captureDevice, observerProxy);
    }

    public static async Task<ObservableCaptureDevice> AsObservableAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        bool isScattering,
        int maxQueuingFrames,
        CancellationToken ct = default)
    {
        var observerProxy = new ObservableCaptureDevice.ObserverProxy();
        var captureDevice = await descriptor.OpenWithFrameProcessorAsync(
            characteristics, transcodeIfYUV,
            isScattering ?
                new DelegatedScatteringProcessor(observerProxy.OnPixelBufferArrived, maxQueuingFrames) :
                new DelegatedQueuingProcessor(observerProxy.OnPixelBufferArrived, maxQueuingFrames),
            ct).
            ConfigureAwait(false);

        return new ObservableCaptureDevice(captureDevice, observerProxy);
    }

    //////////////////////////////////////////////////////////////////////////////////

    public static Task<byte[]> TakeOneShotAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        CancellationToken ct = default) =>
        descriptor.InternalTakeOneShotAsync(characteristics, true, ct);

    public static Task<byte[]> TakeOneShotAsync(
        this CaptureDeviceDescriptor descriptor,
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        CancellationToken ct = default) =>
        descriptor.InternalTakeOneShotAsync(characteristics, transcodeIfYUV, ct);
}
