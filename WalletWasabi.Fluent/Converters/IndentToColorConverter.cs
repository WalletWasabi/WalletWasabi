using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace WalletWasabi.Fluent.Converters;

internal class IndentToColorConverter : IMultiValueConverter
{
	public static IndentToColorConverter Instance { get; } = new();

	public List<IBrush>? Brushes { get; set; }

	public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		var obj = values.Skip(0).First();

		if (obj is null || values.Skip(1).First() is not IRows rows)
		{
			return BindingOperations.DoNothing;
		}

		var rowIndex = FindIndex(rows, el => el.Model == obj);
		var modelIndex = rows.RowIndexToModelIndex(rowIndex);
		return GetBrush(modelIndex.Count - 1) ?? BindingOperations.DoNothing;
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
