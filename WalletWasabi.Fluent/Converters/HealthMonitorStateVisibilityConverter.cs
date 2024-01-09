using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.Converters;

public class HealthMonitorStateVisibilityConverter : IValueConverter
{
	public static readonly HealthMonitorStateVisibilityConverter Instance = new();

	private HealthMonitorStateVisibilityConverter()
	{
	}

	object IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is HealthMonitorState state && parameter is HealthMonitorState paramState)
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
