using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Converters;

public class BoolToBrushConverter : IValueConverter
{
	public static BoolToBrushConverter Instance { get; } = new();

	public IBrush? TrueBrush { get; set; }

	public IBrush? FalseBrush { get; set; }

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is true)
		{
			return TrueBrush;
		}
		else
		{
			return FalseBrush;
		}
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
