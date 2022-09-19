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

	public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		var obj = values.Skip(0).First();
		var rows = values.Skip(1).First() as IRows;

		if (obj is null || rows is null)
		{
			return BindingOperations.DoNothing;
		}

		var rowIndex = FindIndex(rows, el => el.Model == obj);
		var modelIndex = rows.RowIndexToModelIndex(rowIndex);
		return GetBrush(modelIndex.Count -1) ?? BindingOperations.DoNothing;
	}

	private IBrush? GetBrush(int modelIndex)
	{
		if (Brushes is null || modelIndex >= Brushes.Count || modelIndex < 0)
		{
			return null;
		}

		return Brushes[modelIndex];
	}

	public static int FindIndex<T>(IEnumerable<T> source, Func<T, bool> predicate)
	{
		int i = 0;
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

	public List<IBrush>? Brushes { get; set; }
}
