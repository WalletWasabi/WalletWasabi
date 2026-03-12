using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Converters;

public class ConnectionStatusBrushConverter : AvaloniaObject, IValueConverter
{
	public static readonly StyledProperty<IBrush?> SuccessBrushProperty = AvaloniaProperty.Register<ConnectionStatusBrushConverter, IBrush?>(nameof(SuccessBrush));

	public static readonly StyledProperty<IBrush?> FailureBrushProperty = AvaloniaProperty.Register<ConnectionStatusBrushConverter, IBrush?>(nameof(FailureBrush));

	public IBrush? SuccessBrush
	{
		get => GetValue(SuccessBrushProperty);
		set => SetValue(SuccessBrushProperty, value);
	}

	public IBrush? FailureBrush
	{
		get => GetValue(FailureBrushProperty);
		set => SetValue(FailureBrushProperty, value);
	}

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool isSuccess)
		{
			return isSuccess ? SuccessBrush : FailureBrush;
		}

		return AvaloniaProperty.UnsetValue;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
