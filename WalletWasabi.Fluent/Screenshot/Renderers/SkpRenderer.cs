using System.IO;
using Avalonia;
using Avalonia.Controls;
using SkiaSharp;

namespace WalletWasabi.Fluent.Screenshot.Renderers;

public static class SkpRenderer
{
    public static void Render(Control target, Size size, Stream stream, double dpi = 96, bool useDeferredRenderer = false)
    {
        var bounds = SKRect.Create(new SKSize((float)size.Width, (float)size.Height));
        using var pictureRecorder = new SKPictureRecorder();
        using var canvas = pictureRecorder.BeginRecording(bounds);
        target.Measure(size);
        target.Arrange(new Rect(size));
        CanvasRenderer.Render(target, canvas, dpi, useDeferredRenderer);
        using var picture = pictureRecorder.EndRecording();
        picture.Serialize(stream);
    }
}
