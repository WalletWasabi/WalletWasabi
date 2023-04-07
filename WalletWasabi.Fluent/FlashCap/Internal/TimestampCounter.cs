////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.Diagnostics;

namespace FlashCap.Internal;

internal sealed class TimestampCounter
{
    private readonly Stopwatch stopwatch = new();

    public long ElapsedMicroseconds
    {
        get
        {
            // https://stackoverflow.com/questions/6664538/is-stopwatch-elapsedticks-threadsafe
            long tick;
            lock (this.stopwatch)
            {
                tick = this.stopwatch.ElapsedTicks;
            }
            return tick * 1_000_000 / Stopwatch.Frequency;
        }
    }

    public void Restart()
    {
        lock (this.stopwatch)
        {
            this.stopwatch.Reset();
            this.stopwatch.Start();
        }
    }
}
