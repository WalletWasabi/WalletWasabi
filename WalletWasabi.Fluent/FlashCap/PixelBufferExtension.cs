////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlashCap;

public static class PixelBufferExtension
{
#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static byte[] ExtractImage(
        this PixelBuffer pixelBuffer)
    {
        var image = pixelBuffer.InternalExtractImage(
            PixelBuffer.BufferStrategies.CopyWhenDifferentSizeOrReuse);
        Debug.Assert(image.Array!.Length == image.Count);
        return image.Array;
    }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static byte[] CopyImage(
        this PixelBuffer pixelBuffer)
    {
        var image = pixelBuffer.InternalExtractImage(
            PixelBuffer.BufferStrategies.ForceCopy);
        Debug.Assert(image.Array!.Length == image.Count);
        return image.Array;
    }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static ArraySegment<byte> ReferImage(
        this PixelBuffer pixelBuffer) =>
        pixelBuffer.InternalExtractImage(
            PixelBuffer.BufferStrategies.ForceReuse);
}
