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
				new AvaloniaList<int>() {1});

		public static readonly StyledProperty<AvaloniaList<double>> WidthTriggersProperty =
			AvaloniaProperty.Register<ResponsivePanel, AvaloniaList<double>>(nameof(WidthTriggers),
				new AvaloniaList<double>() {0.0});

		public static readonly AttachedProperty<AvaloniaList<int>> ColumnSpanProperty =
			AvaloniaProperty.RegisterAttached<ResponsivePanel, Control, AvaloniaList<int>>("ColumnSpan", new AvaloniaList<int>() {1});

		public static readonly AttachedProperty<AvaloniaList<int>> RowSpanProperty =
			AvaloniaProperty.RegisterAttached<ResponsivePanel, Control, AvaloniaList<int>>("RowSpan", new AvaloniaList<int>() {1});

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

		internal struct Item
		{
			internal int Column;
			internal int Row;
			internal int ColumnSpan;
			internal int RowSpan;
		}

		internal struct State
		{
			internal AvaloniaList<IControl> Children;
			internal double ItemWidth;
			internal double ItemHeight;
			internal double AspectRatio;
			internal AvaloniaList<int> ColumnHints;
			internal AvaloniaList<double> WidthTriggers;
			internal double Width;
			internal double Height;
		}

		internal static bool Validate(ref State state)
		{
			if (state.WidthTriggers is null || state.ColumnHints is null)
			{
				return false;
			}

			if (state.WidthTriggers.Count <= 0)
			{
				// TODO: throw new Exception($"No width trigger specified in {nameof(WidthTriggers)} property.");
				return false;
			}

			if (state.ColumnHints.Count <= 0)
			{
				// No column hints specified in ColumnHints property.
				return false;
			}

			if (state.WidthTriggers.Count != state.ColumnHints.Count)
			{
				// Number of width triggers must be equal to the number of column triggers.
				return false;
			}

			if (double.IsNaN(state.ItemWidth) && double.IsInfinity(state.Width))
			{
				// The ItemWidth can't be NaN and panel width can't be infinity at same time.
				return false;
			}

			if (double.IsNaN(state.ItemHeight) && double.IsInfinity(state.Height))
			{
				// The ItemHeight can't be NaN and panel height can't be infinity at same time.
				return false;
			}

			return true;
		}

		internal static Size MeasureArrange(ref State state, bool isMeasure)
		{
			var layoutIndex = 0;
			var totalColumns = state.ColumnHints[layoutIndex];

			if (!double.IsInfinity(state.Width))
			{
				for (var i = state.WidthTriggers.Count - 1; i >= 0; i--)
				{
					if (state.Width >= state.WidthTriggers[i])
					{
						totalColumns = state.ColumnHints[i];
						layoutIndex = i;
						break;
					}
				}
			}

			var currentColumn = 0;
			var totalRows = 0;
			var rowIncrement = 1;
			var items = new Item[state.Children.Count];

			for (var i = 0; i < state.Children.Count; i++)
			{
				var element = state.Children[i];
				var columnSpan = GetColumnSpan((Control) element)[layoutIndex];
				var rowSpan = GetRowSpan((Control) element)[layoutIndex];

				items[i] = new Item()
				{
					Column = currentColumn,
					Row = totalRows,
					ColumnSpan = columnSpan,
					RowSpan = rowSpan
				};

				rowIncrement = Math.Max(rowSpan, rowIncrement);
				currentColumn += columnSpan;

				if (currentColumn >= totalColumns || i == state.Children.Count - 1)
				{
					currentColumn = 0;
					totalRows += rowIncrement;
					rowIncrement = 1;
				}
			}

			var columnWidth = double.IsNaN(state.ItemWidth) ? state.Width / totalColumns : state.ItemWidth;
			var rowHeight = double.IsNaN(state.ItemHeight)
				? double.IsNaN(state.AspectRatio) ? state.Height / totalRows : columnWidth * state.AspectRatio
				: state.ItemHeight;

			for (var i = 0; i < state.Children.Count; i++)
			{
				var element = state.Children[i];
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

		private Size MeasureArrange(Size panelSize, bool isMeasure)
		{
			var state = new State()
			{
				Children = Children,
				ItemWidth = ItemWidth,
				ItemHeight = ItemHeight,
				AspectRatio = double.IsNaN(AspectRatio) && (panelSize.Height == 0 || double.IsInfinity(panelSize.Height)) ? 1.0 : AspectRatio,
				ColumnHints = ColumnHints,
				WidthTriggers = WidthTriggers,
				Width = double.IsNaN(WidthSource) ? panelSize.Width : WidthSource,
				Height = panelSize.Height
			};

			return !Validate(ref state) ? Size.Empty : MeasureArrange(ref state, isMeasure);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			return MeasureArrange(availableSize, true);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			return MeasureArrange(finalSize, false);
		}
	}
}