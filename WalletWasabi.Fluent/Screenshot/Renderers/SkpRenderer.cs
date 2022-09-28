using System.IO;
using Avalonia;
using Avalonia.Controls;
using SkiaSharp;

namespace WalletWasabi.Fluent.Screenshot.Renderers;

public static class SkpRenderer
{
    public static void Render(Control target, Size size, Stream stream)
    {
        var bounds = SKRect.Create(new SKSize((float)size.Width, (float)size.Height));
        using var pictureRecorder = new SKPictureRecorder();
        using var canvas = pictureRecorder.BeginRecording(bounds);
        target.Measure(size);
        target.Arrange(new Rect(size));
        CanvasRenderer.Render(target, canvas, 96d);
        using var picture = pictureRecorder.EndRecording();
        picture.Serialize(stream);
    }
}
