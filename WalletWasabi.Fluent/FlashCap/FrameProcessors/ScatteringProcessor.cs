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
using System.Threading;
using System.Threading.Tasks;

namespace FlashCap.FrameProcessors;

internal abstract class ScatteringProcessor :
    FrameProcessor
{
    private readonly int maxQueuingFrames;
    private readonly WaitCallback pixelBufferArrivedEntry;

    protected volatile int processing = 1;
    protected volatile bool aborting;
    protected AsyncManualResetEvent final = new();

    protected ScatteringProcessor(int maxQueuingFrames)
    {
        this.maxQueuingFrames = maxQueuingFrames;
        this.pixelBufferArrivedEntry = this.PixelBufferArrivedEntry;
    }

    protected override async Task OnDisposeAsync()
    {
        if (this.final != null)
        {
            Debug.Assert(!this.aborting);

            this.aborting = true;
            if (Interlocked.Decrement(ref this.processing) >= 1)
            {
                await AsyncManualResetEvent.WaitAnyAsync(default, this.final).
                    ConfigureAwait(false);
            }

            this.final = null!;
        }
    }

    public override sealed void OnFrameArrived(
        CaptureDevice captureDevice,
        IntPtr pData, int size,
        long timestampMicroseconds, long frameIndex)
    {
        if (this.aborting)
        {
            return;
        }

        if (Interlocked.Increment(ref this.processing) <= this.maxQueuingFrames)
        {
            var buffer = base.GetPixelBuffer();

            this.Capture(
                captureDevice,
                pData, size,
                timestampMicroseconds, frameIndex,
                buffer);

            ThreadPool.QueueUserWorkItem(
                this.pixelBufferArrivedEntry, buffer);
        }
        else
        {
            Interlocked.Decrement(ref this.processing);
        }
    }

    protected abstract void PixelBufferArrivedEntry(object? parameter);
}

internal sealed class DelegatedScatteringProcessor :
    ScatteringProcessor
{
    private PixelBufferArrivedDelegate pixelBufferArrived;

    public DelegatedScatteringProcessor(
        PixelBufferArrivedDelegate pixelBufferArrived, int maxQueuingFrames) :
        base(maxQueuingFrames) =>
        this.pixelBufferArrived = pixelBufferArrived;

    protected override async Task OnDisposeAsync()
    {
        await base.DisposeAsync().
            ConfigureAwait(false);
        this.pixelBufferArrived = null!;
    }

    protected override void PixelBufferArrivedEntry(object? parameter)
    {
        var buffer = (PixelBuffer)parameter!;

        if (this.aborting)
        {
            if (Interlocked.Decrement(ref base.processing) <= 0)
            {
                base.final?.Set();
            }
            return;
        }

        try
        {
            using var scope = new AutoPixelBufferScope(this, buffer);
            this.pixelBufferArrived(scope);
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
        }
        finally
        {
            if (Interlocked.Decrement(ref base.processing) <= 0)
            {
                base.final?.Set();
            }
        }
    }
}

internal sealed class DelegatedScatteringTaskProcessor :
    ScatteringProcessor
{
    private PixelBufferArrivedTaskDelegate pixelBufferArrived;

    public DelegatedScatteringTaskProcessor(
        PixelBufferArrivedTaskDelegate pixelBufferArrived, int maxQueuingFrames) :
        base(maxQueuingFrames) =>
        this.pixelBufferArrived = pixelBufferArrived;

    protected override async Task OnDisposeAsync()
    {
        await base.DisposeAsync().
            ConfigureAwait(false);
        this.pixelBufferArrived = null!;
    }

    protected override async void PixelBufferArrivedEntry(object? parameter)
    {
        var buffer = (PixelBuffer)parameter!;

        if (this.aborting)
        {
            if (Interlocked.Decrement(ref base.processing) <= 0)
            {
                base.final?.Set();
            }
            return;
        }

        try
        {
            using var scope = new AutoPixelBufferScope(this, buffer);
            await this.pixelBufferArrived(scope).
                ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
        }
        finally
        {
            if (Interlocked.Decrement(ref base.processing) <= 0)
            {
                base.final?.Set();
            }
        }
    }
}
