using Avalonia.Controls;
using Avalonia.Rendering;
using SkiaSharp;

namespace WalletWasabi.Fluent.Screenshot;

public static class CanvasRenderer
{
    public static void Render(Control target, SKCanvas canvas, double dpi)
    {
        using var renderTarget = new CanvasRenderTarget(canvas, dpi);
        ImmediateRenderer.Render(target, renderTarget);
    }
}
