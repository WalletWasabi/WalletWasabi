using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Converters
{
	public static class WalletIconConverter
	{
		public static readonly IValueConverter WalletTypeToImage =
			new FuncValueConverter<WalletType, Bitmap>(GetBitmap);

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

				case WalletType.Normal:
				case WalletType.Unknown:
					uri = new($"avares://WalletWasabi.Fluent/Assets/WalletIcons/{ThemeHelper.CurrentTheme}/normal.png");
					break;
			}

			return AssetHelpers.GetBitmapAsset(uri);
		}
	}
}
