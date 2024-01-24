using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public class BoolToGenericConverter<T> : AvaloniaObject, IValueConverter
{
	public static readonly StyledProperty<T?> TrueProperty =
		AvaloniaProperty.Register<BoolToGenericConverter<T>, T?>(nameof(True));

	public static readonly StyledProperty<T?> FalseProperty =
		AvaloniaProperty.Register<BoolToGenericConverter<T>, T?>(nameof(False));

	public T? True
	{
		get => GetValue(TrueProperty);
		set => SetValue(TrueProperty, value);
	}

	public T? False
	{
		get => GetValue(FalseProperty);
		set => SetValue(FalseProperty, value);
	}

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is true)
		{
			return True ?? AvaloniaProperty.UnsetValue;
		}
		else
		{
			return False ?? AvaloniaProperty.UnsetValue;
		}
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
