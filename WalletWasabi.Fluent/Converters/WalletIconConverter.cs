using System;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Converters
{
	public static class WalletIconConverter
	{
		public static readonly IValueConverter KeyManagerToImage =
			new FuncValueConverter<KeyManager, Bitmap>(km =>
			{
				var type = Enum.TryParse(typeof(WalletType), km.Icon, true, out var typ) && typ is { }
					? (WalletType)typ
					: km.IsHardwareWallet
						? WalletType.Hardware
						: WalletType.Normal;

				return GetBitmap(type);
			});

		public static readonly IValueConverter WalletTypeToImage =
			new FuncValueConverter<WalletType, Bitmap>(GetBitmap);

		public static readonly IValueConverter StringToImage =
			new FuncValueConverter<string, Bitmap>(icon =>
			{
				var type = Enum.TryParse(typeof(WalletType), icon, true, out var typ) && typ is { }
					? (WalletType)typ
					: WalletType.Normal;

				return GetBitmap(type);
			});

		private static Bitmap GetBitmap(WalletType type)
		{
			Uri uri = new("avares://WalletWasabi.Fluent/Assets/HardwareIcons/generic.png");

			switch (type)
			{
				case WalletType.Coldcard:
					uri = new("avares://WalletWasabi.Fluent/Assets/HardwareIcons/coldcard.png");
					break;

				case WalletType.Trezor:
					uri = new("avares://WalletWasabi.Fluent/Assets/HardwareIcons/trezor.png");
					break;

				case WalletType.Ledger:
					uri = new("avares://WalletWasabi.Fluent/Assets/HardwareIcons/ledger.png");
					break;

				case WalletType.Normal:
					uri = new("avares://WalletWasabi.Fluent/Assets/HardwareIcons/normal.png");
					break;
			}

			var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();

			using var image = assets.Open(uri);
			return new Bitmap(image);
		}
	}
}