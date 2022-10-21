using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public class BooleanConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool b)
		{
			if (b)
			{
				return TrueValue;
			}
			else
			{
				return FalseValue;
			}
		}

		return AvaloniaProperty.UnsetValue;
	}

	public object? TrueValue { get; set; }

	public object? FalseValue { get; set; }

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
