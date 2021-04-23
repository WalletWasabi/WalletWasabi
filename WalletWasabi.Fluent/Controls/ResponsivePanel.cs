using System;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls
{
	public class ResponsivePanel : Panel
	{
		public static readonly StyledProperty<double> ItemWidthProperty =
			AvaloniaProperty.Register<ResponsivePanel, double>(nameof(ItemWidth), double.NaN);

		public static readonly StyledProperty<double> ItemHeightProperty =
			AvaloniaProperty.Register<ResponsivePanel, double>(nameof(ItemHeight), double.NaN);

		public static readonly StyledProperty<double> WidthSourceProperty =
			AvaloniaProperty.Register<ResponsivePanel, double>(nameof(WidthSource), double.NaN);

		public static readonly StyledProperty<double> AspectRatioProperty =
			AvaloniaProperty.Register<ResponsivePanel, double>(nameof(AspectRatio), double.NaN);

		public static readonly StyledProperty<AvaloniaList<int>> ColumnHintsProperty =
			AvaloniaProperty.Register<ResponsivePanel, AvaloniaList<int>>(nameof(ColumnHints),
				new AvaloniaList<int>() { 1 });

		public static readonly StyledProperty<AvaloniaList<double>> WidthTriggersProperty =
			AvaloniaProperty.Register<ResponsivePanel, AvaloniaList<double>>(nameof(WidthTriggers),
				new AvaloniaList<double>() { 0.0 });

		public static readonly AttachedProperty<AvaloniaList<int>> ColumnSpanProperty =
			AvaloniaProperty.RegisterAttached<ResponsivePanel, Control, AvaloniaList<int>>("ColumnSpan", new AvaloniaList<int>() { 1 });

		public static readonly AttachedProperty<AvaloniaList<int>> RowSpanProperty =
			AvaloniaProperty.RegisterAttached<ResponsivePanel, Control, AvaloniaList<int>>("RowSpan", new AvaloniaList<int>() { 1 });

		public static AvaloniaList<int> GetColumnSpan(Control? element)
		{
			Contract.Requires<ArgumentNullException>(element != null);
			return element!.GetValue(ColumnSpanProperty);
		}

		public static void SetColumnSpan(Control? element, AvaloniaList<int> value)
		{
			Contract.Requires<ArgumentNullException>(element != null);
			element!.SetValue(ColumnSpanProperty, value);
		}

		public static AvaloniaList<int> GetRowSpan(Control? element)
		{
			Contract.Requires<ArgumentNullException>(element != null);
			return element!.GetValue(RowSpanProperty);
		}

		public static void SetRowSpan(Control? element, AvaloniaList<int> value)
		{
			Contract.Requires<ArgumentNullException>(element != null);
			element!.SetValue(RowSpanProperty, value);
		}

		public double ItemWidth
		{
			get => GetValue(ItemWidthProperty);
			set => SetValue(ItemWidthProperty, value);
		}

		public double ItemHeight
		{
			get => GetValue(ItemHeightProperty);
			set => SetValue(ItemHeightProperty, value);
		}

		public double WidthSource
		{
			get => GetValue(WidthSourceProperty);
			set => SetValue(WidthSourceProperty, value);
		}

		public double AspectRatio
		{
			get => GetValue(AspectRatioProperty);
			set => SetValue(AspectRatioProperty, value);
		}

		public AvaloniaList<int> ColumnHints
		{
			get => GetValue(ColumnHintsProperty);
			set => SetValue(ColumnHintsProperty, value);
		}

		public AvaloniaList<double> WidthTriggers
		{
			get => GetValue(WidthTriggersProperty);
			set => SetValue(WidthTriggersProperty, value);
		}

		static ResponsivePanel()
		{
			AffectsParentMeasure<ResponsivePanel>(
				ItemWidthProperty,
				ItemHeightProperty,
				WidthSourceProperty,
				AspectRatioProperty,
				ColumnHintsProperty,
				WidthTriggersProperty,
				ColumnSpanProperty,
				RowSpanProperty);
			AffectsParentArrange<ResponsivePanel>(
				ItemWidthProperty,
				ItemHeightProperty,
				WidthSourceProperty,
				AspectRatioProperty,
				ColumnHintsProperty,
				WidthTriggersProperty,
				ColumnSpanProperty,
				RowSpanProperty);
			AffectsMeasure<ResponsivePanel>(
				ItemWidthProperty,
				ItemHeightProperty,
				WidthSourceProperty,
				AspectRatioProperty,
				ColumnHintsProperty,
				WidthTriggersProperty,
				ColumnSpanProperty,
				RowSpanProperty);
			AffectsArrange<ResponsivePanel>(
				ItemWidthProperty,
				ItemHeightProperty,
				WidthSourceProperty,
				AspectRatioProperty,
				ColumnHintsProperty,
				WidthTriggersProperty,
				ColumnSpanProperty,
				RowSpanProperty);
		}

		private struct Item
		{
			internal int Column;
			internal int Row;
			internal int ColumnSpan;
			internal int RowSpan;
		}

		private Size MeasureArrange(Size panelSize, bool isMeasure)
		{
			var children = Children;
			var widthTriggers = WidthTriggers;
			var columnHints = ColumnHints;
			var aspectRatio = AspectRatio;
			var width = double.IsNaN(WidthSource) ? panelSize.Width : WidthSource;
			var height = panelSize.Height;

			if (widthTriggers is null || columnHints is null)
			{
				return Size.Empty;
			}

			if (widthTriggers.Count <= 0)
			{
				// TODO: throw new Exception($"No width trigger specified in {nameof(WidthTriggers)} property.");
				return Size.Empty;
			}

			if (columnHints.Count <= 0)
			{
				// TODO: throw new Exception($"No column hints specified in {nameof(ColumnHints)} property.");
				return Size.Empty;
			}

			if (widthTriggers.Count != columnHints.Count)
			{
				// TODO: throw new Exception($"Number of width triggers must be equal to the number of column triggers.");
				return Size.Empty;
			}

			if (double.IsNaN(ItemWidth) && double.IsInfinity(width))
			{
				// TODO: throw new Exception($"The {nameof(ItemWidth)} can't be NaN and panel {nameof(width)} can't be infinity at same time.");
				return Size.Empty;
			}

			if (double.IsNaN(ItemHeight) && double.IsInfinity(height))
			{
				// TODO: throw new Exception($"The {nameof(ItemHeight)} can't be NaN and panel {nameof(height)} can't be infinity at same time.");
				return Size.Empty;
			}

			if (double.IsNaN(aspectRatio) && (height == 0 || double.IsInfinity(height)))
			{
				aspectRatio = 1.0;
			}

			var layoutIndex = 0;
			var totalColumns = columnHints[layoutIndex];

			if (!double.IsInfinity(width))
			{
				for (var i = widthTriggers.Count - 1; i >= 0; i--)
				{
					if (width >= widthTriggers[i])
					{
						totalColumns = columnHints[i];
						layoutIndex = i;
						break;
					}
				}
			}

			var currentColumn = 0;
			var totalRows = 0;
			var rowIncrement = 1;
			var items = new Item[children.Count];

			for (var i = 0; i < children.Count; i++)
			{
				var element = children[i];
				var columnSpan = GetColumnSpan((Control)element)[layoutIndex];
				var rowSpan = GetRowSpan((Control)element)[layoutIndex];

				items[i] = new Item()
				{
					Column = currentColumn,
					Row = totalRows,
					ColumnSpan = columnSpan,
					RowSpan = rowSpan
				};

				rowIncrement = Math.Max(rowSpan, rowIncrement);
				currentColumn += columnSpan;

				if (currentColumn >= totalColumns || i == children.Count - 1)
				{
					currentColumn = 0;
					totalRows += rowIncrement;
					rowIncrement = 1;
				}
			}

			var itemWidth = double.IsNaN(ItemWidth) ? width / totalColumns : ItemWidth;
			var itemHeight = double.IsNaN(ItemHeight)
				? double.IsNaN(aspectRatio) ? height / totalRows : itemWidth * aspectRatio
				: ItemHeight;

			for (var i = 0; i < children.Count; i++)
			{
				var element = children[i];
				var size = new Size(itemWidth * items[i].ColumnSpan, itemHeight * items[i].RowSpan);
				var position = new Point(items[i].Column * itemWidth, items[i].Row * itemHeight);
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

			// Console.WriteLine($"CxR: {totalColumns}x{totalRows} Item WxH: {itemWidth}x{itemHeight} Panel WxH: {itemWidth * totalColumns}x{itemHeight * totalRows}");
			return new Size(itemWidth * totalColumns, itemHeight * totalRows);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			var measureSize = MeasureArrange(availableSize, true);
			// Console.WriteLine($"MeasureOverride: {measureSize}");
			return measureSize;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			var arrangeSize = MeasureArrange(finalSize, false);
			// Console.WriteLine($"ArrangeOverride: {arrangeSize}");
			return arrangeSize;
		}
	}
}