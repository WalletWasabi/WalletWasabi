////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FlashCap;

public static class CaptureDeviceExtension
{
#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static Task StartAsync(this CaptureDevice captureDevice, CancellationToken ct = default) =>
        captureDevice.InternalStartAsync(ct);

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static Task StopAsync(this CaptureDevice captureDevice, CancellationToken ct = default) =>
        captureDevice.InternalStopAsync(ct);

    [Obsolete("Start method will be deprecated. Switch to use StartAsync method.")]
    public static void Start(this CaptureDevice captureDevice) =>
        _ = captureDevice.InternalStartAsync(default);

    [Obsolete("Stop method will be deprecated. Switch to use StopAsync method.")]
    public static void Stop(this CaptureDevice captureDevice) =>
        _ = captureDevice.InternalStopAsync(default);
}
