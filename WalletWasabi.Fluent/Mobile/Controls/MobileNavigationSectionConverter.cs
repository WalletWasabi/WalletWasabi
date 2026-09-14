using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace WalletWasabi.Fluent.Mobile.Controls;

/// <summary>Maps a page to its parent navigation tab without changing route identity.</summary>
public sealed class MobileNavigationSectionConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not string section || parameter is not string tab) return false;
		var parent = section switch
		{
			"transaction" => "history",
			"coinjoin" or "coins" => "privacy",
			_ => section
		};
		return string.Equals(parent, tab, StringComparison.Ordinal);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}
