using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

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
				new() { Name = "Png", Extensions = { "png" } },
				new() { Name = "All", Extensions = { "*" } }
			},
			InitialFileName = "WalletWasabi",
			DefaultExtension = "png"
		};

		var result = await dlg.ShowAsync(window);
		if (result is { } path)
		{
			Save(root, root.Bounds.Size, path);
		}
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
		}
	}
}
