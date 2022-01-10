using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Converters;

public class StatusBarStateVisibilityConverter : IValueConverter
{
	public static readonly StatusBarStateVisibilityConverter Instance = new();

	private StatusBarStateVisibilityConverter()
	{
	}

	object IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is StatusBarState state && parameter is StatusBarState paramState)
		{
			return state == paramState;
		}

		return AvaloniaProperty.UnsetValue;
	}

	object IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
