using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Converters;

public class StatusIconStateVisibilityConverter : IValueConverter
{
	public static readonly StatusIconStateVisibilityConverter Instance = new();

	private StatusIconStateVisibilityConverter()
	{
	}

	object IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is StatusIconState state && parameter is StatusIconState paramState)
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
