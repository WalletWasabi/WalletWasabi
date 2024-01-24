using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Converters;

internal class IndentToBrushConverter : IMultiValueConverter
{
	public List<IBrush>? Brushes { get; set; }

	public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count != 2)
		{
			return AvaloniaProperty.UnsetValue;
		}

		var model = values[0];

		if (values[1] is not IRows rows)
		{
			return AvaloniaProperty.UnsetValue;
		}

		var rowIndex = FindIndex(rows, el => el.Model == model);
		var modelIndex = rows.RowIndexToModelIndex(rowIndex);
		return GetBrush(modelIndex.Count - 1) ?? AvaloniaProperty.UnsetValue;
	}

	public static int FindIndex<T>(IEnumerable<T> source, Func<T, bool> predicate)
	{
		var i = 0;
		foreach (var item in source)
		{
			if (predicate(item))
			{
				return i;
			}

			i++;
		}

		return -1;
	}

	private IBrush? GetBrush(int modelIndex)
	{
		if (Brushes is null || modelIndex >= Brushes.Count || modelIndex < 0)
		{
			return null;
		}

		return Brushes[modelIndex];
	}
}
