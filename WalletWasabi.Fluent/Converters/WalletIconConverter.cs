using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Converters;

public class WalletIconConverter : IValueConverter
{
	public static readonly IValueConverter WalletTypeToImage = new WalletIconConverter();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
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

	private static IImage? GetBitmap(WalletType type)
	{
		if (Application.Current!.Styles.TryGetResource($"WalletIcon_{type}", ThemeHelper.CurrentTheme == Theme.Dark ? ThemeVariant.Dark : ThemeVariant.Light, out var v) && v is DrawingGroup g)
		{
			return new DrawingImage(g);
		}

		return null;
	}
}
