////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FlashCap.Internal;

internal sealed class AsyncManualResetEvent
{
    private volatile TaskCompletionSource<bool>? tcs;

    public void Set()
    {
        var tcs = Interlocked.Exchange(
            ref this.tcs,
            null);
        tcs?.TrySetResult(true);
    }

    public void Reset()
    {
        if (this.tcs == null)
        {
            Interlocked.CompareExchange(
                ref this.tcs,
                new TaskCompletionSource<bool>(),
                null);
        }
    }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1
    public async ValueTask WaitAsync(CancellationToken ct)
#else
    public async Task WaitAsync(CancellationToken ct)
#endif
    {
        if (this.tcs is { } tcs)
        {
            using var _ = ct.Register(() => tcs.TrySetCanceled());
            await tcs.Task.
                ConfigureAwait(false);
        }
    }

#if NETCOREAPP || NETSTANDARD2_1
    public static async ValueTask<int> WaitAnyAsync(
        CancellationToken ct, params AsyncManualResetEvent[] evs)
#else
    public static async Task<int> WaitAnyAsync(
        CancellationToken ct, params AsyncManualResetEvent[] evs)
#endif
    {
        var captured = new Task[evs.Length];

        while (true)
        {
            for (var index = 0; index < captured.Length; index++)
            {
                if (evs[index].tcs is { } tcs)
                {
                    captured[index] = tcs.Task;
                }
                else
                {
                    return index;
                }
            }

            var result = await TaskCompat.WhenAny(captured).
                ConfigureAwait(false);
            for (var index = 0; index < captured.Length; index++)
            {
                if (object.ReferenceEquals(captured[index], result))
                {
                    return index;
                }
            }

            Debug.Assert(false);
        }
    }
}
