////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace FlashCap.Utilities;

public sealed class PixelFormatComparer : IComparer<PixelFormats>
{
    private PixelFormatComparer()
    {
    }

    private static int GetComparableCode(PixelFormats pixelFormat) =>
        pixelFormat switch
        {
            PixelFormats.RGB8 => 0,
            PixelFormats.RGB16 => 10,
            PixelFormats.JPEG => 20,
            PixelFormats.RGB24 => 40,
            PixelFormats.RGB32 => 50,
            PixelFormats.ARGB32 => 60,
            PixelFormats.PNG => 70,
            _ => 30,
        };

    public int Compare(PixelFormats x, PixelFormats y) =>
        GetComparableCode(x).CompareTo(GetComparableCode(y));

    public static readonly PixelFormatComparer Instance =
        new PixelFormatComparer();
}
