using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace WalletWasabi.Fluent.Screenshot.Renderers;

public static class PngRenderer
{
    public static void Render(Control target, Size size, Stream stream)
    {
        var pixelSize = new PixelSize((int)size.Width, (int)size.Height);
        var dpiVector = new Vector(96d, 96d);
        using var bitmap = new RenderTargetBitmap(pixelSize, dpiVector);
        target.Measure(size);
        target.Arrange(new Rect(size));
        bitmap.Render(target);
        bitmap.Save(stream);
    }
}
