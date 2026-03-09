using Avalonia.Data.Converters;
using System.Globalization;

namespace WalletWasabi.Fluent.Converters;

public class BoolStringConverter : IValueConverter
{
	public string True { get; set; } = "True";
	public string False { get; set; } = "False";

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not bool boolValue)
		{
			return False;
		}

		return boolValue ? True : False;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
