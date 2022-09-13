using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Skia.Helpers;
using SkiaSharp;

namespace WalletWasabi.Fluent.Screenshot;

public class CanvasRenderTarget : IRenderTarget
{
    private readonly SKCanvas _canvas;
    private readonly double _dpi;

    public CanvasRenderTarget(SKCanvas canvas, double dpi)
    {
        _canvas = canvas;
        _dpi = dpi;
    }

    public IDrawingContextImpl CreateDrawingContext(IVisualBrushRenderer? visualBrushRenderer)
    {
        return DrawingContextHelper.WrapSkiaCanvas(_canvas, new Vector(_dpi, _dpi), visualBrushRenderer);
    }

    public void Dispose()
    {
    }
}
