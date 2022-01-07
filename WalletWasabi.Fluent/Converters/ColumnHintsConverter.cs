using System.Globalization;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using Avalonia.Utilities;

namespace WalletWasabi.Fluent.Converters;

public class ColumnHintsConverter : IValueConverter
{
	public static readonly ColumnHintsConverter Instance = new();

	private ColumnHintsConverter()
	{
	}

		object? IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is AvaloniaList<int> columnHints)
		{
			return string.Join(",", columnHints.Select(x => x.ToString(CultureInfo.InvariantCulture)));
		}

		return null;
	}

		object? IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string str)
		{
			var columnHints = new AvaloniaList<int>();
			var values = str.Split(',');

			foreach (var s in values)
			{
					if (TypeUtilities.TryConvert(typeof(int), s, culture, out var v) && v is { })
				{
					columnHints.Add((int)v);
				}
				else
				{
					// throw new InvalidCastException($"Could not convert '{s}' to {typeof(int)}.");
					return null;
				}
			}

			return columnHints;
		}

		return null;
	}
}
