using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class PlainTextColumn<T> : ColumnBase<T>
{
	private readonly Func<T, string?> _getter;
	private readonly Comparison<T?>? _sortAscending;
	private readonly Comparison<T?>? _sortDescending;

	public PlainTextColumn(
		object? header,
		Func<T, string?> getter,
		GridLength? width,
		ColumnOptions<T>? options)
		: base(header, width, options)
	{
		_sortAscending = options?.CompareAscending;
		_sortDescending = options?.CompareDescending;
		_getter = getter;
	}

	public override ICell CreateCell(IRow<T> row)
	{
		return new PlainTextCell(_getter(row.Model));
	}

	public override Comparison<T?>? GetComparison(ListSortDirection direction)
	{
		return direction switch
		{
			ListSortDirection.Ascending => _sortAscending,
			ListSortDirection.Descending => _sortDescending,
			_ => null,
		};
	}
}
