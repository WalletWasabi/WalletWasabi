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
				new() { Name = "Xps", Extensions = { "xps" } },
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

	private static void Save(Control? control, Size size, string path)
	{
		if (control is null)
		{
			return;
		}

		var extension = Path.GetExtension(path);
		switch (extension.ToLower())
		{
			case ".png":
			{
				using var stream = File.Create(path);
				PngRenderer.Render(control, size, stream);
				break;
			}
			case ".svg":
			{
				using var stream = File.Create(path);
				SvgRenderer.Render(control, size, stream);
				break;
			}
			case ".pdf":
			{
				using var stream = File.Create(path);
				PdfRenderer.Render(control, size, stream);
				break;
			}
			case ".skp":
			{
				using var stream = File.Create(path);
				SkpRenderer.Render(control, size, stream);
				break;
			}
		}
	}
}
