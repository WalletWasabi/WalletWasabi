using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WalletWasabi.Fluent.Helpers;

public static class AssetHelpers
{
	public static Bitmap GetBitmapAsset(Uri uri)
	{
		var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
		using var image = assets.Open(uri);
		return new Bitmap(image);
	}

	public static Bitmap GetBitmapAsset(string path)
	{
		Uri uri = new(path);
		return GetBitmapAsset(uri);
	}
}
