using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Metadata;

namespace WalletWasabi.Fluent.Converters;

/// <summary>
///     Converts a dictionary key to an arbitrary content
/// </summary>
public class KeyToDictionaryItemConverter : AvaloniaObject, IValueConverter
{
	public static readonly StyledProperty<ResourceDictionary?> DictionaryProperty = AvaloniaProperty.Register<KeyToDictionaryItemConverter, ResourceDictionary?>(nameof(Dictionary));

	[Content]
	public ResourceDictionary? Dictionary
	{
		get => GetValue(DictionaryProperty);
		set => SetValue(DictionaryProperty, value);
	}

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string s)
		{
			return Dictionary?[s] ?? value;
		}

		return value;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
