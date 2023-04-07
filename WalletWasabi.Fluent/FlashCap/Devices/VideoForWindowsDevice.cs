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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;

namespace FlashCap.Devices;

public sealed class VideoForWindowsDevice : CaptureDevice
{
    private readonly TimestampCounter counter = new();
    private int deviceIndex;
    private bool transcodeIfYUV;
    private FrameProcessor frameProcessor;
    private long frameIndex;

    private IndependentSingleApartmentContext? workingContext = new();
    private IntPtr handle;
    private GCHandle thisPin;
    private NativeMethods_VideoForWindows.CAPVIDEOCALLBACK? callback;
    private IntPtr pBih;

#pragma warning disable CS8618
    internal VideoForWindowsDevice(object identity, string name) :
        base(identity, name)
#pragma warning restore CS8618
    {
    }

    protected override unsafe Task OnInitializeAsync(
        VideoCharacteristics characteristics,
        bool transcodeIfYUV,
        FrameProcessor frameProcessor,
        CancellationToken ct)
    {
        this.deviceIndex = (int)this.Identity;
        this.Characteristics = characteristics;
        this.transcodeIfYUV = transcodeIfYUV;
        this.frameProcessor = frameProcessor;

        if (!NativeMethods.GetCompressionAndBitCount(
            characteristics.PixelFormat, out var compression, out var bitCount))
        {
            throw new ArgumentException(
                $"FlashCap: Couldn't set video format [1]: DeviceIndex={this.deviceIndex}");
        }

        return this.workingContext!.InvokeAsync(() =>
        {
            var handle = NativeMethods_VideoForWindows.CreateVideoSourceWindow(this.deviceIndex);
            if (handle == IntPtr.Zero)
            {
                throw new ArgumentException(
                    $"FlashCap: Couldn't allocate video source window: DeviceIndex={this.deviceIndex}");
            }
            try
            {
                NativeMethods_VideoForWindows.capDriverConnect(handle, this.deviceIndex);
                try
                {
                    NativeMethods_VideoForWindows.capSetPreviewScale(handle, false);
                    NativeMethods_VideoForWindows.capSetPreviewFPS(handle, 15);
                    NativeMethods_VideoForWindows.capSetOverlay(handle, true);

                    ///////////////////////////////////////

                    // At first set 5fps, because can't set both fps and video format atomicity.
                    NativeMethods_VideoForWindows.capCaptureGetSetup(handle, out var cp);
                    cp.dwRequestMicroSecPerFrame = 1_000_000 / 5;   // 5fps
                    if (!NativeMethods_VideoForWindows.capCaptureSetSetup(handle, cp))
                    {
                        throw new ArgumentException(
                            $"FlashCap: Couldn't set video frame rate [1]: DeviceIndex={this.deviceIndex}");
                    }
                    NativeMethods_VideoForWindows.capCaptureGetSetup(handle, out cp);

                    var pih = NativeMethods.AllocateMemory((IntPtr)sizeof(NativeMethods.BITMAPINFOHEADER));
                    try
                    {
                        var pBih = (NativeMethods.BITMAPINFOHEADER*)pih.ToPointer();

                        pBih->biSize = sizeof(NativeMethods.BITMAPINFOHEADER);
                        pBih->biCompression = compression;
                        pBih->biPlanes = 1;
                        pBih->biBitCount = bitCount;
                        pBih->biWidth = characteristics.Width;
                        pBih->biHeight = characteristics.Height;
                        pBih->biSizeImage = pBih->CalculateImageSize();

                        // Try to set video format.
                        if (!NativeMethods_VideoForWindows.capSetVideoFormat(handle, pih))
                        {
                            throw new ArgumentException(
                                $"FlashCap: Couldn't set video format [2]: DeviceIndex={this.deviceIndex}");
                        }

                        // Try to set fps, but VFW API may cause ignoring it silently...
                        cp.dwRequestMicroSecPerFrame =
                            (int)(1_000_000 / characteristics.FramesPerSecond);
                        if (!NativeMethods_VideoForWindows.capCaptureSetSetup(handle, cp))
                        {
                            throw new ArgumentException(
                                $"FlashCap: Couldn't set video frame rate [2]: DeviceIndex={this.deviceIndex}");
                        }
                        NativeMethods_VideoForWindows.capCaptureGetSetup(handle, out cp);
                    }
                    finally
                    {
                        NativeMethods.FreeMemory(pih);
                    }

                    // Get final video format.
                    NativeMethods_VideoForWindows.capGetVideoFormat(handle, out this.pBih);
                }
                catch
                {
                    NativeMethods_VideoForWindows.capDriverDisconnect(handle, this.deviceIndex);
                    throw;
                }
            }
            catch
            {
                NativeMethods_VideoForWindows.DestroyWindow(handle);
                throw;
            }

                    ///////////////////////////////////////

                    this.handle = handle;

                    // https://stackoverflow.com/questions/4097235/is-it-necessary-to-gchandle-alloc-each-callback-in-a-class
                    this.thisPin = GCHandle.Alloc(this, GCHandleType.Normal);
                    this.callback = this.CallbackEntry;

                    NativeMethods_VideoForWindows.capSetCallbackFrame(handle, this.callback);
        }, ct);
    }

    ~VideoForWindowsDevice()
    {
        if (this.handle != IntPtr.Zero)
        {
            var handle = this.handle;
            this.handle = IntPtr.Zero;

            this.workingContext?.Post(_ =>
            {
                NativeMethods_VideoForWindows.capSetCallbackFrame(handle, null);
                NativeMethods_VideoForWindows.capDriverDisconnect(handle, this.deviceIndex);
                NativeMethods_VideoForWindows.DestroyWindow(handle);

                this.thisPin.Free();
                this.callback = null;
                NativeMethods.FreeMemory(this.pBih);
                this.pBih = IntPtr.Zero;

                workingContext.Dispose();
            }, null);
        }
        else
        {
            this.workingContext?.Dispose();
        }
    }

    protected override async Task OnDisposeAsync()
    {
        if (this.handle != IntPtr.Zero)
        {
            var handle = this.handle;
            this.handle = IntPtr.Zero;

            try
            {
                await this.frameProcessor.DisposeAsync().
                    ConfigureAwait(false);
            }
            catch
            {
            }

            try
            {
                await this.OnStopAsync(default).
                    ConfigureAwait(false);
            }
            catch
            {
            }

            var workingContext = this.workingContext!;
            this.workingContext = null;

            workingContext.Post(_ =>
            {
                NativeMethods_VideoForWindows.capSetCallbackFrame(handle, null);
                NativeMethods_VideoForWindows.capDriverDisconnect(handle, this.deviceIndex);
                NativeMethods_VideoForWindows.DestroyWindow(handle);

                this.thisPin.Free();
                this.callback = null;
                NativeMethods.FreeMemory(this.pBih);
                this.pBih = IntPtr.Zero;

                workingContext.Dispose();
            }, null);
        }
        else
        {
            this.workingContext?.Dispose();
        }
    }

    private void CallbackEntry(
        IntPtr hWnd, in NativeMethods_VideoForWindows.VIDEOHDR hdr)
    {
        // HACK: Avoid stupid camera devices...
        if (hdr.dwBytesUsed >= 64)
        {
            try
            {
                this.frameProcessor.OnFrameArrived(
                    this,
                    hdr.lpData, hdr.dwBytesUsed,
                    // HACK: `hdr.dwTimeCaptured` always zero on my environment...
                    this.counter.ElapsedMicroseconds,
                    this.frameIndex++);
            }
            // DANGER: Stop leaking exception around outside of unmanaged area...
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
    }

    protected override Task OnStartAsync(CancellationToken ct)
    {
        if (!this.IsRunning)
        {
            return this.workingContext!.InvokeAsync(() =>
            {
                this.frameIndex = 0;
                this.counter.Restart();
                NativeMethods_VideoForWindows.capShowPreview(this.handle, true);
                this.IsRunning = true;
            }, default);
        }
        else
        {
            return TaskCompat.CompletedTask;
        }
    }

    protected override Task OnStopAsync(CancellationToken ct)
    {
        if (this.IsRunning)
        {
            return this.workingContext!.InvokeAsync(() =>
            {
                this.IsRunning = false;
                NativeMethods_VideoForWindows.capShowPreview(this.handle, false);
            }, default);
        }
        else
        {
            return TaskCompat.CompletedTask;
        }
    }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    protected override void OnCapture(
        IntPtr pData, int size, long timestampMicroseconds, long frameIndex,
        PixelBuffer buffer) =>
        buffer.CopyIn(this.pBih, pData, size, timestampMicroseconds, frameIndex, this.transcodeIfYUV);
}
