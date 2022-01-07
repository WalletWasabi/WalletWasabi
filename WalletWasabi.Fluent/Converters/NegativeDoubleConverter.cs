using System.Globalization;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public class NegativeDoubleConverter : IValueConverter
{
	public static readonly NegativeDoubleConverter Instance = new();

	private NegativeDoubleConverter()
	{
	}

		object? IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is double d)
		{
			return -d;
		}

		return null;
	}

		object? IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
			return null;
	}
}
