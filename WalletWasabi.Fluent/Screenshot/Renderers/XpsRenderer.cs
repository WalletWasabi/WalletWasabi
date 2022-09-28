using System.IO;
using Avalonia;
using Avalonia.Controls;
using SkiaSharp;

namespace WalletWasabi.Fluent.Screenshot.Renderers;

public static class XpsRenderer
{
    public static void Render(Control target, Size size, Stream stream)
    {
        using var managedWStream = new SKManagedWStream(stream);
        using var document = SKDocument.CreateXps(stream);
        using var canvas = document.BeginPage((float)size.Width, (float)size.Height);
        target.Measure(size);
        target.Arrange(new Rect(size));
        CanvasRenderer.Render(target, canvas, 72d);
    }
}
