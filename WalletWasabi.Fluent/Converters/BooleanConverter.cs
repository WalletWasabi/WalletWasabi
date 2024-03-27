using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

public class BooleanConverter : AvaloniaObject, IValueConverter
{
	public static readonly StyledProperty<object> FalseValueProperty = AvaloniaProperty.Register<BooleanConverter, object>(nameof(FalseValue));

	public static readonly StyledProperty<object> TrueValueProperty = AvaloniaProperty.Register<BooleanConverter, object>(nameof(TrueValue));

	public object FalseValue
	{
		get => GetValue(FalseValueProperty);
		set => SetValue(FalseValueProperty, value);
	}

	public object TrueValue
	{
		get => GetValue(TrueValueProperty);
		set => SetValue(TrueValueProperty, value);
	}

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool isTrue)
		{
			if (isTrue)
			{
				return TrueValue;
			}

			return FalseValue;
		}

		return AvaloniaProperty.UnsetValue;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
