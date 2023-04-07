////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlashCap;

public sealed class ObservableCaptureDevice :
    IObservable<PixelBufferScope>, IDisposable
{
    private CaptureDevice captureDevice;
    private ObserverProxy proxy;

    internal ObservableCaptureDevice(CaptureDevice captureDevice, ObserverProxy proxy)
    {
        this.captureDevice = captureDevice;
        this.proxy = proxy;
    }

    public void Dispose()
    {
        if (this.captureDevice is { } captureDevice)
        {
            captureDevice.Dispose();
            this.captureDevice = null!;
        }
        if (this.proxy is { } proxy)
        {
            proxy.InternalDispose();
            this.proxy = null!;
        }
    }

    public VideoCharacteristics Characteristics =>
        this.captureDevice.Characteristics;
    public bool IsRunning =>
        this.captureDevice.IsRunning;

    internal Task InternalStartAsync(CancellationToken ct) =>
        this.captureDevice.InternalStartAsync(ct);
    internal Task InternalStopAsync(CancellationToken ct) =>
        this.captureDevice.InternalStopAsync(ct);

    internal IDisposable InternalSubscribe(IObserver<PixelBufferScope> observer)
    {
        this.proxy.Subscribe(observer);
        return this.proxy;
    }

    IDisposable IObservable<PixelBufferScope>.Subscribe(IObserver<PixelBufferScope> observer) =>
        this.InternalSubscribe(observer);

    internal sealed class ObserverProxy : IDisposable
    {
        private volatile IObserver<PixelBufferScope>? observer;
        private volatile bool isShutdown;

        public void Subscribe(IObserver<PixelBufferScope> observer)
        {
            if (Interlocked.CompareExchange(ref this.observer, observer, null) != null)
            {
                throw new InvalidOperationException();
            }
        }

        internal void InternalDispose()
        {
            this.isShutdown = true;
            Interlocked.Exchange(ref this.observer, null)?.OnCompleted();
        }

        public void Dispose() =>
            Interlocked.Exchange(ref this.observer, null);

        public void OnPixelBufferArrived(PixelBufferScope bufferScope)
        {
            if (this.observer is { } observer)
            {
                try
                {
                    observer.OnNext(bufferScope);
                }
                finally
                {
                    if (this.isShutdown)
                    {
                        Interlocked.Exchange(ref this.observer, null)?.OnCompleted();
                    }
                }
            }
        }
    }
}
