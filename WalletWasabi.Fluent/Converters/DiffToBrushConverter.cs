using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Converters;

public class DiffToBrushConverter : AvaloniaObject, IValueConverter
{
	public static readonly StyledProperty<IBrush?> PositiveBrushProperty = AvaloniaProperty.Register<DiffToBrushConverter, IBrush?>(nameof(PositiveBrush));

	public static readonly StyledProperty<IBrush?> NegativeBrushProperty = AvaloniaProperty.Register<DiffToBrushConverter, IBrush?>(nameof(NegativeBrush));

	public static readonly StyledProperty<IBrush?> ZeroBrushProperty = AvaloniaProperty.Register<DiffToBrushConverter, IBrush?>(nameof(ZeroBrush));

	public IBrush? PositiveBrush
	{
		get => GetValue(PositiveBrushProperty);
		set => SetValue(PositiveBrushProperty, value);
	}

	public IBrush? NegativeBrush
	{
		get => GetValue(NegativeBrushProperty);
		set => SetValue(NegativeBrushProperty, value);
	}

	public IBrush? ZeroBrush
	{
		get => GetValue(ZeroBrushProperty);
		set => SetValue(ZeroBrushProperty, value);
	}

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return AvaloniaProperty.UnsetValue;
		}

		if (value is double || value is int || value is float || value is decimal)
		{
			var number = System.Convert.ToDouble(value);
			if (number > 0)
			{
				return PositiveBrush;
			}

			if (number < 0)
			{
				return NegativeBrush;
			}

			return ZeroBrush;
		}

		return AvaloniaProperty.UnsetValue;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
