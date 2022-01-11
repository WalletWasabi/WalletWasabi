using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.Controls;

internal class ResponsivePanelState
{
	private readonly IReadOnlyList<ILayoutable> _children;

	public ResponsivePanelState(IReadOnlyList<ILayoutable> children)
	{
		_children = children;
	}

	public double ItemWidth { get; set; }

	public double ItemHeight { get; set; }

	public double AspectRatio { get; set; }

	public IList<int>? ColumnHints { get; set; }

	public IList<double>? WidthTriggers { get; set; }

	public double Width { get; set; }

	public double Height { get; set; }

	public int ItemCount => _children.Count;

	public ILayoutable GetItemAt(int index) => _children[index];

	public bool Validate()
	{
		if (WidthTriggers is null || ColumnHints is null)
		{
			return false;
		}

		if (WidthTriggers.Count <= 0)
		{
			// TODO: throw new Exception($"No width trigger specified in {nameof(WidthTriggers)} property.");
			return false;
		}

		if (ColumnHints.Count <= 0)
		{
			// No column hints specified in ColumnHints property.
			return false;
		}

		if (WidthTriggers.Count != ColumnHints.Count)
		{
			// Number of width triggers must be equal to the number of column triggers.
			return false;
		}

		if (double.IsNaN(ItemWidth) && double.IsInfinity(Width))
		{
			// The ItemWidth can't be NaN and panel width can't be infinity at same time.
			return false;
		}

		if (double.IsNaN(ItemHeight) && double.IsInfinity(Height))
		{
			// The ItemHeight can't be NaN and panel height can't be infinity at same time.
			return false;
		}

		return true;
	}

	public Size MeasureArrange(bool isMeasure)
	{
		if (ColumnHints is null)
		{
			return Size.Empty;
		}

		var layoutIndex = 0;
		var totalColumns = ColumnHints[layoutIndex];

		if (!double.IsInfinity(Width) && WidthTriggers is { })
		{
			for (var i = WidthTriggers.Count - 1; i >= 0; i--)
			{
				if (Width >= WidthTriggers[i])
				{
					totalColumns = ColumnHints[i];
					layoutIndex = i;
					break;
				}
			}
		}

		var currentColumn = 0;
		var totalRows = 0;
		var rowIncrement = 1;
		var items = new Item[ItemCount];

		for (var i = 0; i < ItemCount; i++)
		{
			var element = GetItemAt(i);
			var columnSpanPropertyValue = NonVirtualizingResponsiveLayout.GetColumnSpan((Control)element);
			var rowSpanPropertyValue = NonVirtualizingResponsiveLayout.GetRowSpan((Control)element);
			var columnSpan = columnSpanPropertyValue is not null && columnSpanPropertyValue.Count - 1 >= layoutIndex ? columnSpanPropertyValue[layoutIndex] : 1;
			var rowSpan = rowSpanPropertyValue is not null && rowSpanPropertyValue.Count - 1 >= layoutIndex ? rowSpanPropertyValue[layoutIndex] : 1;

			items[i] = new Item()
			{
				Column = currentColumn,
				Row = totalRows,
				ColumnSpan = columnSpan,
				RowSpan = rowSpan
			};

			rowIncrement = Math.Max(rowSpan, rowIncrement);
			currentColumn += columnSpan;

			if (currentColumn >= totalColumns || i == ItemCount - 1)
			{
				currentColumn = 0;
				totalRows += rowIncrement;
				rowIncrement = 1;
			}
		}

		var columnWidth = double.IsNaN(ItemWidth) ? Width / totalColumns : ItemWidth;
		var rowHeight = double.IsNaN(ItemHeight)
			? double.IsNaN(AspectRatio) ? Height / totalRows : columnWidth * AspectRatio
			: ItemHeight;

		for (var i = 0; i < ItemCount; i++)
		{
			var element = GetItemAt(i);
			var size = new Size(columnWidth * items[i].ColumnSpan, rowHeight * items[i].RowSpan);
			var position = new Point(items[i].Column * columnWidth, items[i].Row * rowHeight);
			var rect = new Rect(position, size);

			if (isMeasure)
			{
				element.Measure(size);
			}
			else
			{
				element.Arrange(rect);
			}
		}

		return new Size(columnWidth * totalColumns, rowHeight * totalRows);
	}

	private struct Item
	{
		internal int Column;
		internal int Row;
		internal int ColumnSpan;
		internal int RowSpan;
	}
}
