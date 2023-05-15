using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WalletWasabi.Fluent.Helpers;

public static class AssetHelpers
{
	public static Bitmap GetBitmapAsset(Uri uri)
	{
		using var image = AssetLoader.Open(uri);
		return new Bitmap(image);
	}

	public static Bitmap GetBitmapAsset(string path)
	{
		return GetBitmapAsset(new Uri(path));
	}
}
