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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FlashCap;

public abstract class FrameProcessor
{
    private readonly Stack<PixelBuffer> reserver = new();

    protected FrameProcessor()
    {
    }

    [Obsolete("Dispose method overriding is obsoleted. Switch OnDisposeAsync instead.", true)]
    protected virtual void Dispose() =>
        throw new InvalidOperationException();

    protected abstract Task OnDisposeAsync();

    public async Task DisposeAsync()
    {
        try
        {
            await this.OnDisposeAsync().
                ConfigureAwait(false);
        }
        finally
        {
            lock (this.reserver)
            {
                this.reserver.Clear();
            }
        }
    }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    protected PixelBuffer GetPixelBuffer()
    {
        PixelBuffer? buffer = null;
        lock (this.reserver)
        {
            if (this.reserver.Count >= 1)
            {
                buffer = this.reserver.Pop();
            }
        }
        if (buffer == null)
        {
            buffer = new PixelBuffer();
        }
        return buffer;
    }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public void ReleasePixelBuffer(PixelBuffer buffer)
    {
        lock (this.reserver)
        {
            this.reserver.Push(buffer);
        }
    }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    protected void Capture(CaptureDevice captureDevice,
        IntPtr pData, int size,
        long timestampMicroseconds, long frameIndex,
        PixelBuffer buffer) =>
        captureDevice.InternalOnCapture(pData, size, timestampMicroseconds, frameIndex, buffer);

    public abstract void OnFrameArrived(
        CaptureDevice captureDevice,
        IntPtr pData, int size, long timestampMicroseconds, long frameIndex);

    protected sealed class AutoPixelBufferScope :
        PixelBufferScope, IDisposable
    {
        private FrameProcessor? parent;

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public AutoPixelBufferScope(
            FrameProcessor parent,
            PixelBuffer buffer) :
            base(buffer) =>
            this.parent = parent;

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Dispose()
        {
            lock (this)
            {
                if (this.parent is { } parent)
                {
                    base.OnReleaseNow();
                    this.parent.ReleasePixelBuffer(this.Buffer);
                    this.parent = null;
                }
            }
        }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        protected override void OnReleaseNow() =>
            this.Dispose();
    }
}
