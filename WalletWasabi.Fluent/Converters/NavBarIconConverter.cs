using Avalonia;
using Avalonia.Data.Converters;
using System.Globalization;

namespace WalletWasabi.Fluent.Converters;

public class NavBarIconConverter : IValueConverter
{
	public static readonly NavBarIconConverter Instance = new();

	private NavBarIconConverter()
	{
	}

	object IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (Application.Current is { } && value is string iconName)
		{
			if (Application.Current.Styles.TryGetResource(iconName, out object? resource))
			{
				return resource ?? AvaloniaProperty.UnsetValue;
			}
		}

		return AvaloniaProperty.UnsetValue;
	}

	object IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
