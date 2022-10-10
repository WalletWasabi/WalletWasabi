using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;

namespace WalletWasabi.Fluent.Extensions;

public static class TreeDataGridExtensions
{
	public static int ModelToRowIndex<T>(this HierarchicalTreeDataGridSource<T> source, T model, Func<T, IEnumerable<T>?> getChildren) where T : class
	{
		var node = source.Items
			.ToTreeNodes(getChildren)
			.Flatten(x => x.Children)
			.FirstOrDefault(x => Equals(x.Item, model));

		if (node is null)
		{
			return -1;
		}

		var modelIndex = new IndexPath(node.Path);
		return source.Rows.ModelIndexToRowIndex(modelIndex);
	}

	public static void BringIntoView<T>(this Avalonia.Controls.TreeDataGrid treeDataGrid, T model, Func<T, IEnumerable<T>> getChildren) where T : class
	{
		if (treeDataGrid is { RowsPresenter: { Items: { } } rowsPresenter, Source: HierarchicalTreeDataGridSource<T> source })
		{
			var node = source.Items
				.ToTreeNodes(getChildren)
				.Flatten(x => x.Children)
				.FirstOrDefault(x => Equals(x.Item, model));

			if (node is null)
			{
				return;
			}

			ExpandPath<T>(source, node.Path);

			var index = ModelToRowIndex(source, model, getChildren);

			rowsPresenter.BringIntoView(index);
		}
	}

	public static void ExpandPath<T>(this ITreeDataGridSource source, IEnumerable<int> modelPath)
	{
		var paths = Grow(modelPath);

		foreach (var path in paths.SkipLast(1))
		{
			var rowId = source.Rows.ModelIndexToRowIndex(new IndexPath(path));
			var row = (HierarchicalRow<T>) source.Rows[rowId];
			row.IsExpanded = true;
		}
	}

	public static IEnumerable<IEnumerable<T>> Grow<T>(IEnumerable<T> sequence)
	{
		return sequence.Select((x, i) => sequence.Take(i + 1));
	}
}
