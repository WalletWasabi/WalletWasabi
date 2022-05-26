using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WalletWasabi.Fluent.Helpers;

public static class AssetHelpers
{
	public static Bitmap GetBitmapAsset(Uri uri)
	{
		var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();

		if (assets is { })
		{
			using var image = assets.Open(uri);
			return new Bitmap(image);
		}

		throw new Exception("Program is not initialised or is in an inconsistent state.");
	}

	public static Bitmap GetBitmapAsset(string path)
	{
		return GetBitmapAsset(new Uri(path));
	}
}
