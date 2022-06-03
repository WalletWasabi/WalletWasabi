using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;

namespace WalletWasabi.Fluent.TreeDataGrid;

internal class DiscreetTextColumn<T> : ColumnBase<T>
{
	private readonly Func<T, string?> _getter;
	private readonly Comparison<T?>? _sortAscending;
	private readonly Comparison<T?>? _sortDescending;
	private readonly int _numberOfDiscreetChars;

	public DiscreetTextColumn(
		object? header,
		Func<T, string?> getter,
		GridLength? width,
		ColumnOptions<T>? options,
		int numberOfDiscreetChars = 0)
		: base(header, width, options)
	{
		_sortAscending = options?.CompareAscending;
		_sortDescending = options?.CompareDescending;
		_getter = getter;
		_numberOfDiscreetChars = numberOfDiscreetChars;
	}

	public override ICell CreateCell(IRow<T> row)
	{
		return new DiscreetTextCell(_getter(row.Model), _numberOfDiscreetChars);
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
