using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Converters;

/// <summary>
/// A selector clears its selection while its items are being replaced; this keeps that from reaching the bound property.
/// </summary>
public class IgnoreEmptySelectionConverter : IValueConverter
{
	public static readonly IgnoreEmptySelectionConverter Instance = new();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value;

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value ?? BindingOperations.DoNothing;
}
