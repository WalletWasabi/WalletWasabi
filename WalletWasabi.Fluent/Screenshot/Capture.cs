using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Skia.Helpers;
using SkiaSharp;

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
		if (root is not Window window)
		{
			return;
		}

		var dlg = new SaveFileDialog
		{
			Title = "Save screenshot",
			Filters = new()
			{
				new() { Name = "Svg", Extensions = { "svg" } },
				new() { Name = "Png", Extensions = { "png" } },
				new() { Name = "Pdf", Extensions = { "pdf" } },
				new() { Name = "Skp", Extensions = { "skp" } },
				new() { Name = "All", Extensions = { "*" } }
			},
			InitialFileName = "WalletWasabi",
			DefaultExtension = "svg"
		};

		var result = await dlg.ShowAsync(window);
		if (result is { } path)
		{
			Save(root, root.Bounds.Size, path);
		}
	}

	private static void Render(Control target, SKCanvas canvas, double dpi)
	{
		using var renderTarget = new CanvasRenderTarget(canvas, dpi);
		ImmediateRenderer.Render(target, renderTarget);
	}

	private static void Save(Control? target, Size size, string path)
	{
		if (target is null)
		{
			return;
		}

		var extension = Path.GetExtension(path);
		switch (extension.ToLower())
		{
			case ".png":
				{
					using var stream = File.Create(path);
					var pixelSize = new PixelSize((int)size.Width, (int)size.Height);
					var dpiVector = new Vector(96d, 96d);
					using var bitmap = new RenderTargetBitmap(pixelSize, dpiVector);
					target.Measure(size);
					target.Arrange(new Rect(size));
					bitmap.Render(target);
					bitmap.Save(stream);
					break;
				}
			case ".svg":
				{
					using var stream = File.Create(path);
					using var managedWStream = new SKManagedWStream(stream);
					var bounds = SKRect.Create(new SKSize((float)size.Width, (float)size.Height));
					using var canvas = SKSvgCanvas.Create(bounds, managedWStream);
					target.Measure(size);
					target.Arrange(new Rect(size));
					Render(target, canvas, 96d);
					break;
				}
			case ".pdf":
				{
					using var stream = File.Create(path);
					using var managedWStream = new SKManagedWStream(stream);
					using var document = SKDocument.CreatePdf(stream, 72f);
					using var canvas = document.BeginPage((float)size.Width, (float)size.Height);
					target.Measure(size);
					target.Arrange(new Rect(size));
					Render(target, canvas, 72f);
					break;
				}
			case ".skp":
				{
					using var stream = File.Create(path);
					var bounds = SKRect.Create(new SKSize((float)size.Width, (float)size.Height));
					using var pictureRecorder = new SKPictureRecorder();
					using var canvas = pictureRecorder.BeginRecording(bounds);
					target.Measure(size);
					target.Arrange(new Rect(size));
					Render(target, canvas, 96d);
					using var picture = pictureRecorder.EndRecording();
					picture.Serialize(stream);
					break;
				}
		}
	}

	private class CanvasRenderTarget : IRenderTarget
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
}
