using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public class IndentConverter : AvaloniaObject, IValueConverter
{
	public double Multiplier { get; set; }

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is int indent)
		{
			return new Thickness(indent * Multiplier, 0, 0, 0);
		}

		return new Thickness();
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
