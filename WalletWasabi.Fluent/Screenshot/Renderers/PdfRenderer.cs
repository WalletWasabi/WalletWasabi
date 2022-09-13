using System.IO;
using Avalonia;
using Avalonia.Controls;
using SkiaSharp;

namespace WalletWasabi.Fluent.Screenshot.Renderers;

public static class PdfRenderer
{
    public static void Render(Control target, Size size, Stream stream, double dpi = 72, bool useDeferredRenderer = false)
    {
        using var managedWStream = new SKManagedWStream(stream);
        using var document = SKDocument.CreatePdf(stream, (float)dpi);
        using var canvas = document.BeginPage((float)size.Width, (float)size.Height);
        target.Measure(size);
        target.Arrange(new Rect(size));
        CanvasRenderer.Render(target, canvas, dpi, useDeferredRenderer);
    }
}
