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

namespace FlashCap;

public sealed class PixelBuffer
{
    private byte[]? imageContainer;
    private int imageContainerSize;
    private byte[]? transcodedImageContainer = null;
    private bool isValidTranscodedImage;
    private long timestampMicroseconds;
    private bool transcodeIfYUV;

    internal PixelBuffer()
    {
    }

    internal unsafe void CopyIn(
        IntPtr pih, IntPtr pData, int size,
        long timestampMicroseconds, long frameIndex,
        bool transcodeIfYUV)
    {
        this.FrameIndex = frameIndex;
        this.timestampMicroseconds = timestampMicroseconds;

        var pBih = (NativeMethods.BITMAPINFOHEADER*)pih.ToPointer();

        var totalSize = pBih->biCompression switch
        {
            NativeMethods.Compression.MJPG => size,
            NativeMethods.Compression.BI_JPEG => size,
            NativeMethods.Compression.BI_PNG => size,
            _ => sizeof(NativeMethods.BITMAPFILEHEADER) +
                pBih->biSize + size
        };

        lock (this)
        {
            if (this.imageContainer == null ||
                this.imageContainer.Length < totalSize)
            {
                Debug.WriteLine($"Allocated: CurrentSize={this.imageContainer?.Length ?? 0}, Size={totalSize}");

                this.imageContainer = new byte[totalSize];
            }

            this.imageContainerSize = totalSize;
            this.isValidTranscodedImage = false;

            fixed (byte* pImageContainer = this.imageContainer!)
            {
                if (pBih->biCompression == NativeMethods.Compression.MJPG ||
                    pBih->biCompression == NativeMethods.Compression.BI_JPEG ||
                    pBih->biCompression == NativeMethods.Compression.BI_PNG)
                {
                    NativeMethods.CopyMemory(
                        (IntPtr)pImageContainer,
                        pData,
                        (IntPtr)size);

                    this.transcodeIfYUV = false;
                }
                else
                {
                    var pBfhTo = (NativeMethods.BITMAPFILEHEADER*)pImageContainer;
                    pBfhTo->bfType0 = 0x42;
                    pBfhTo->bfType1 = 0x4d;
                    pBfhTo->bfSize = totalSize;

                    pBfhTo->bfOffBits =
                        sizeof(NativeMethods.BITMAPFILEHEADER) +
                        pBih->biSize;

                    var pBihTo = (NativeMethods.BITMAPINFOHEADER*)(pBfhTo + 1);

                    NativeMethods.CopyMemory(
                        (IntPtr)pBihTo,
                        (IntPtr)pBih,
                        (IntPtr)(pBih->biSize));

                    NativeMethods.CopyMemory(
                        (IntPtr)(pImageContainer + pBfhTo->bfOffBits),
                        pData,
                        (IntPtr)size);

                    this.transcodeIfYUV = transcodeIfYUV;
                }
            }
        }
    }

    public TimeSpan Timestamp =>
        TimeSpan.FromMilliseconds(this.timestampMicroseconds / 1000.0);

    public long FrameIndex { get; private set; }

    internal enum BufferStrategies
    {
        ForceCopy,
        CopyWhenDifferentSizeOrReuse,
        ForceReuse,
    }

    internal unsafe ArraySegment<byte> InternalExtractImage(BufferStrategies strategy)
    {
        lock (this)
        {
            if (this.imageContainer == null)
            {
                throw new InvalidOperationException("Extracted before capture.");
            }

            if (this.transcodeIfYUV)
            {
                if (this.isValidTranscodedImage && this.transcodedImageContainer != null)
                {
                    if (strategy == BufferStrategies.ForceReuse)
                    {
                        return new ArraySegment<byte>(this.transcodedImageContainer);
                    }
                    else
                    {
                        var copied1 = new byte[this.transcodedImageContainer.Length];
                        Array.Copy(this.transcodedImageContainer, copied1, copied1.Length);
                        return new ArraySegment<byte>(copied1);
                    }
                }

                fixed (byte* pImageContainer = this.imageContainer)
                {
                    var pBfh = (NativeMethods.BITMAPFILEHEADER*)pImageContainer;
                    var pBih = (NativeMethods.BITMAPINFOHEADER*)(pBfh + 1);

                    if (BitmapTranscoder.GetRequiredBufferSize(
                        pBih->biWidth, pBih->biHeight, pBih->biCompression) is { } sizeImage)
                    {
                        var totalSize =
                            sizeof(NativeMethods.BITMAPFILEHEADER) +
                            sizeof(NativeMethods.BITMAPINFOHEADER) +
                            sizeImage;

                        if (this.transcodedImageContainer == null ||
                            this.transcodedImageContainer.Length != totalSize)
                        {
                            this.transcodedImageContainer = new byte[totalSize];
                        }

                        fixed (byte* pTranscodedImageContainer = this.transcodedImageContainer)
                        {
                            var pBfhTo = (NativeMethods.BITMAPFILEHEADER*)pTranscodedImageContainer;
                            var pBihTo = (NativeMethods.BITMAPINFOHEADER*)(pBfhTo + 1);

                            pBfhTo->bfType0 = 0x42;
                            pBfhTo->bfType1 = 0x4d;
                            pBfhTo->bfSize = totalSize;

                            pBfhTo->bfOffBits =
                                sizeof(NativeMethods.BITMAPFILEHEADER) +
                                sizeof(NativeMethods.BITMAPINFOHEADER);

                            pBihTo->biSize = sizeof(NativeMethods.BITMAPINFOHEADER);
                            pBihTo->biWidth = pBih->biWidth;
                            pBihTo->biHeight = pBih->biHeight;
                            pBihTo->biPlanes = 1;
                            pBihTo->biBitCount = 24;   // RGB888
                            pBihTo->biCompression = NativeMethods.Compression.BI_RGB;
                            pBihTo->biSizeImage = sizeImage;
#if DEBUG
                            var sw = new Stopwatch();
                            sw.Start();
#endif
                            BitmapTranscoder.Transcode(
                                pBih->biWidth, pBih->biHeight,
                                pBih->biCompression, false,
                                pImageContainer + pBfh->bfOffBits,
                                pTranscodedImageContainer + pBfhTo->bfOffBits);

#if DEBUG
                            Debug.WriteLine($"Transcoded: Elapsed={sw.Elapsed}");
#endif
                        }

                        if (strategy == BufferStrategies.ForceReuse)
                        {
                            this.isValidTranscodedImage = true;
                            return new ArraySegment<byte>(this.transcodedImageContainer);
                        }
                        else
                        {
                            var copied1 = this.transcodedImageContainer;
                            this.transcodedImageContainer = null;
                            return new ArraySegment<byte>(copied1);
                        }
                    }
                }
            }

            switch (strategy)
            {
                case BufferStrategies.ForceReuse:
                    return new ArraySegment<byte>(this.imageContainer, 0, this.imageContainerSize);
                case BufferStrategies.CopyWhenDifferentSizeOrReuse:
                    if (this.imageContainer.Length == this.imageContainerSize)
                    {
                        return new ArraySegment<byte>(this.imageContainer);
                    }
                    break;
            }

            var copied = new byte[this.imageContainerSize];
            Array.Copy(this.imageContainer, copied, copied.Length);

            Debug.WriteLine($"Copied: CurrentSize={this.imageContainer.Length}, Size={this.imageContainerSize}");

            return new ArraySegment<byte>(copied);
        }
    }
}
