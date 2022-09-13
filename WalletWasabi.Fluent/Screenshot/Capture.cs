using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WalletWasabi.Fluent.Screenshot.Renderers;

namespace WalletWasabi.Fluent.Screenshot;

public static class Capture
{
    public static void AttachCapture(this TopLevel root)
    {
        AttachCapture(root, new(Key.F6));
    }

    public static void AttachCapture(this TopLevel root, KeyGesture gesture)
    {
        async void Handler(object? sender, KeyEventArgs args)
        {
            if (gesture.Matches(args))
            {
                await SaveAsync(root);
            }
        }

        root.AddHandler(InputElement.KeyDownEvent, Handler, RoutingStrategies.Tunnel);
    }

    private static async Task SaveAsync(TopLevel root)
    {
        var dlg = new SaveFileDialog { Title = "Save screenshot" };
        dlg.Filters = new()
        {
	        new() {Name = "Svg", Extensions = {"svg"}},
	        new() {Name = "Png", Extensions = {"png"}},
	        new() { Name = "Pdf", Extensions = { "pdf" } },
	        new() { Name = "Xps", Extensions = { "xps" } },
	        new() { Name = "Skp", Extensions = { "skp" } },
	        new() { Name = "All", Extensions = { "*" } }
        };
        dlg.InitialFileName = "WalletWasabi";
        dlg.DefaultExtension = "svg";
        if (root is not Window window)
        {
            return;
        }
        var result = await dlg.ShowAsync(window);
        if (result is { } path)
        {
            Save(root, root.Bounds.Size, path);
        }
    }

    private static void Save(Control? control, Size size, string path)
    {
        if (control is null)
        {
            return;
        }

        if (path.EndsWith("png", StringComparison.OrdinalIgnoreCase))
        {
            PngRenderer.Render(control, size, path);
        }

        if (path.EndsWith("svg", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.Create(path);
            SvgRenderer.Render(control, size, stream);
        }

        if (path.EndsWith("pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.Create(path);
            PdfRenderer.Render(control, size, stream, 96);
        }

        if (path.EndsWith("xps", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.Create(path);
            XpsRenderer.Render(control, size, stream, 96);
        }

        if (path.EndsWith("skp", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = File.Create(path);
            SkpRenderer.Render(control, size, stream);
        }
    }
}
