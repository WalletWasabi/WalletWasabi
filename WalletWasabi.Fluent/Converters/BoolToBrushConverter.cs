using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Converters;

public class BoolToBrushConverter : AvaloniaObject, IValueConverter
{
	public static BoolToBrushConverter Instance { get; } = new();

	public static readonly StyledProperty<IBrush?> TrueBrushProperty =
		AvaloniaProperty.Register<BoolToBrushConverter, IBrush?>(nameof(TrueBrush));

	public static readonly StyledProperty<IBrush?> FalseBrushProperty =
		AvaloniaProperty.Register<BoolToBrushConverter, IBrush?>(nameof(FalseBrush));

	public IBrush? TrueBrush
	{
		get => GetValue(TrueBrushProperty);
		set => SetValue(TrueBrushProperty, value);
	}

	public IBrush? FalseBrush
	{
		get => GetValue(FalseBrushProperty);
		set => SetValue(FalseBrushProperty, value);
	}

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is true)
		{
			return TrueBrush ?? AvaloniaProperty.UnsetValue;
		}
		else
		{
			return FalseBrush ?? AvaloniaProperty.UnsetValue;
		}
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
