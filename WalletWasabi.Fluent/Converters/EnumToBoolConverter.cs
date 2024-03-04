// Based on: https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/Converters/EnumToBoolConverter.cs

using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

/// <summary>
/// Converter to convert an enum value to bool by comparing to the given parameter.
/// Both value and parameter must be of the same enum type.
/// </summary>
/// <remarks>
/// This converter is useful to enable binding of radio buttons with a selected enum value.
/// </remarks>
public class EnumToBoolConverter : IValueConverter
{
	public static EnumToBoolConverter Instance = new();

	/// <inheritdoc/>
	public object? Convert(
		object? value,
		Type targetType,
		object? parameter,
		CultureInfo culture)
	{
		if (value == null && parameter == null)
		{
			return true;
		}

		if (value == null || parameter == null)
		{
			return false;
		}

		return value.Equals(parameter);
	}

	/// <inheritdoc/>
	public object? ConvertBack(
		object? value,
		Type targetType,
		object? parameter,
		CultureInfo culture)
	{
		return value is true ? parameter : BindingOperations.DoNothing;
	}
}
