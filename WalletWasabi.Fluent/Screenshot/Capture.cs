using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using WalletWasabi.Fluent.Helpers;

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
		var file = await FileDialogHelper.SaveFileAsync(
			"Save screenshot...",
			new[] { "png", "*" },
			"WalletWasabi.png",
			Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
		if (file is not null)
		{
			Save(root, root.Bounds.Size, file.Path.AbsolutePath);
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
