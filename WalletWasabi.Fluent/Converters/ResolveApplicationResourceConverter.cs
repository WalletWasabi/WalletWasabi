using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

// Not sure if there is a straight XAML-only way to do this
// {StaticResource ResourceKey={Binding SomeString}} doesn't work :(
public class ResolveApplicationResourceConverter : IValueConverter
{
	public static readonly ResolveApplicationResourceConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (Application.Current is null)
		{
			return null;
		}

		if (value is not string key || string.IsNullOrEmpty(key))
		{
			return null;
		}

		return
			Application.Current.Styles.TryGetResource(key, out var resource)
			? resource
			: throw new InvalidOperationException($"Resource '{key}' not found.");
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotImplementedException();
}