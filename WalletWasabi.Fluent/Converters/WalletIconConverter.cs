using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Converters;

public class WalletIconConverter : IValueConverter
{
	public static readonly IValueConverter WalletTypeToImage = new WalletIconConverter();

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is WalletType type)
		{
			try
			{
				return GetBitmap(type);
			}
			catch
			{
				// ignored
			}
		}

		return AvaloniaProperty.UnsetValue;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}

	private static Bitmap GetBitmap(WalletType type)
	{
		Uri uri = new($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/generic.png");

		switch (type)
		{
			case WalletType.Coldcard:
				uri = new($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/coldcard.png");
				break;

			case WalletType.Trezor:
				uri = new($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/trezor.png");
				break;

			case WalletType.Ledger:
				uri = new($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/ledger.png");
				break;

			case WalletType.BitBox:
				uri = new($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/bitbox.png");
				break;

			case WalletType.Jade:
				uri = new($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/jade.png");
				break;

			case WalletType.Normal:
			case WalletType.Unknown:
				uri = new($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/normal.png");
				break;
		}

		return AssetHelpers.GetBitmapAsset(uri);
	}
}
