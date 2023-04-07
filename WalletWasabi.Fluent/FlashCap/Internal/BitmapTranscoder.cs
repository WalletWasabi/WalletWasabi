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
using System.Threading.Tasks;

namespace FlashCap.Internal;

internal static class BitmapTranscoder
{
    private static readonly int scatteringBase = Environment.ProcessorCount;

    // Preffered article: https://docs.microsoft.com/en-us/windows/win32/medfound/recommended-8-bit-yuv-formats-for-video-rendering#420-formats-16-bits-per-pixel

    private static unsafe void TranscodeFromUYVY(
        int width, int height, bool performFullRange,
        byte* pFrom, byte* pTo)
    {
        var scatter = height / scatteringBase;
        Parallel.For(0, (height + scatter - 1) / scatter, ys =>
        {
            var y = ys * scatter;
            var myi = Math.Min(height - y, scatter);

            for (var yi = 0; yi < myi; yi++)
            {
                byte* pFromBase = pFrom + (height - (y + yi) - 1) * width * 2;
                byte* pToBase = pTo + (y + yi) * width * 3;

                for (var x = 0; x < width; x += 2)
                {
                    var d = pFromBase[0] - 128;  // U
                    var c1 = pFromBase[1] - 16;  // Y1
                    var e = pFromBase[2] - 128;  // V
                    var c2 = pFromBase[3] - 16;  // Y2

                    var cc1 = 298 * c1;
                    var cc2 = 298 * c2;

                    *pToBase++ = Clip((cc1 + 516 * d + 128) >> 8);   // B1
                    *pToBase++ = Clip((cc1 - 100 * d - 208 * e + 128) >> 8);   // G1
                    *pToBase++ = Clip((cc1 + 409 * e + 128) >> 8);   // R1

                    *pToBase++ = Clip((cc2 + 516 * d + 128) >> 8);   // B1
                    *pToBase++ = Clip((cc2 - 100 * d - 208 * e + 128) >> 8);   // G1
                    *pToBase++ = Clip((cc2 + 409 * e + 128) >> 8);   // R1

                    pFromBase += 4;
                }
            }
        });
    }

    private static unsafe void TranscodeFromYUYV(
        int width, int height, bool performFullRange,
        byte* pFrom, byte* pTo)
    {
        var scatter = height / scatteringBase;
        Parallel.For(0, (height + scatter - 1) / scatter, ys =>
        {
            var y = ys * scatter;
            var myi = Math.Min(height - y, scatter);

            for (var yi = 0; yi < myi; yi++)
            {
                byte* pFromBase = pFrom + (height - (y + yi) - 1) * width * 2;
                byte* pToBase = pTo + (y + yi) * width * 3;

                for (var x = 0; x < width; x += 2)
                {
                    var c1 = pFromBase[0] - 16;   // Y1
                    var d = pFromBase[1] - 128;   // U
                    var c2 = pFromBase[2] - 16;   // Y2
                    var e = pFromBase[3] - 128;   // V

                    var cc1 = 298 * c1;
                    var cc2 = 298 * c2;

                    *pToBase++ = Clip((cc1 + 516 * d + 128) >> 8);   // B1
                    *pToBase++ = Clip((cc1 - 100 * d - 208 * e + 128) >> 8);   // G1
                    *pToBase++ = Clip((cc1 + 409 * e + 128) >> 8);   // R1

                    *pToBase++ = Clip((cc2 + 516 * d + 128) >> 8);   // B1
                    *pToBase++ = Clip((cc2 - 100 * d - 208 * e + 128) >> 8);   // G1
                    *pToBase++ = Clip((cc2 + 409 * e + 128) >> 8);   // R1

                    pFromBase += 4;
                }
            }
        });
    }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private static byte Clip(int value) =>
        value < 0 ? (byte)0 :
        value > 255 ? (byte)255 :
        (byte)value;

    public static int? GetRequiredBufferSize(
        int width, int height, NativeMethods.Compression compression)
    {
        switch (compression)
        {
            case NativeMethods.Compression.UYVY:
            case NativeMethods.Compression.YUYV:
            case NativeMethods.Compression.YUY2:
                return width * height * 3;
            default:
                return null;
        }
    }

    public static unsafe void Transcode(
        int width, int height,
        NativeMethods.Compression compression, bool performFullRange,
        byte* pFrom, byte* pTo)
    {
        switch (compression)
        {
            case NativeMethods.Compression.UYVY:
                TranscodeFromUYVY(width, height, performFullRange, pFrom, pTo);
                break;
            case NativeMethods.Compression.YUYV:
            case NativeMethods.Compression.YUY2:
                TranscodeFromYUYV(width, height, performFullRange, pFrom, pTo);
                break;
            default:
                throw new ArgumentException();
        }
    }
}
