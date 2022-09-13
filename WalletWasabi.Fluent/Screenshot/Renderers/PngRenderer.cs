using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace WalletWasabi.Fluent.Screenshot.Renderers;

public static class PngRenderer
{
    public static void Render(Control target, Size size, string path, double dpi = 96)
    {
        var pixelSize = new PixelSize((int)size.Width, (int)size.Height);
        var dpiVector = new Vector(dpi, dpi);
        using var bitmap = new RenderTargetBitmap(pixelSize, dpiVector);
        target.Measure(size);
        target.Arrange(new Rect(size));
        bitmap.Render(target);
        bitmap.Save(path);
    }
}
