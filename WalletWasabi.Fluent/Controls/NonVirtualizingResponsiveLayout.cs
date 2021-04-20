using System;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;

namespace WalletWasabi.Fluent.Controls
{
    public class NonVirtualizingResponsiveLayout : NonVirtualizingLayout
    {
	    public static readonly StyledProperty<double> ItemWidthProperty =
		    ResponsivePanel.ItemWidthProperty.AddOwner<NonVirtualizingResponsiveLayout>();

	    public static readonly StyledProperty<double> ItemHeightProperty =
		    ResponsivePanel.ItemHeightProperty.AddOwner<NonVirtualizingResponsiveLayout>();

	    public static readonly StyledProperty<double> WidthSourceProperty =
		    ResponsivePanel.WidthSourceProperty.AddOwner<NonVirtualizingResponsiveLayout>();

	    public static readonly StyledProperty<double> AspectRatioProperty =
		    ResponsivePanel.AspectRatioProperty.AddOwner<NonVirtualizingResponsiveLayout>();

	    public static readonly StyledProperty<AvaloniaList<int>> ColumnHintsProperty =
		    ResponsivePanel.ColumnHintsProperty.AddOwner<NonVirtualizingResponsiveLayout>();

	    public static readonly StyledProperty<AvaloniaList<double>> WidthTriggersProperty =
		    ResponsivePanel.WidthTriggersProperty.AddOwner<NonVirtualizingResponsiveLayout>();

	    public static readonly AttachedProperty<AvaloniaList<int>> ColumnSpanProperty =
		    ResponsivePanel.ColumnSpanProperty.AddOwner<NonVirtualizingResponsiveLayout>();

	    public static readonly AttachedProperty<AvaloniaList<int>> RowSpanProperty =
		    ResponsivePanel.RowSpanProperty.AddOwner<NonVirtualizingResponsiveLayout>();

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

		private Size MeasureArrange(Size panelSize, NonVirtualizingLayoutContext context, bool isMeasure)
		{
			var state = new ResponsivePanelState()
			{
				Children = context.Children,
				ItemWidth = ItemWidth,
				ItemHeight = ItemHeight,
				AspectRatio = double.IsNaN(AspectRatio) && (panelSize.Height == 0 || double.IsInfinity(panelSize.Height)) ? 1.0 : AspectRatio,
				ColumnHints = ColumnHints,
				WidthTriggers = WidthTriggers,
				Width = double.IsNaN(WidthSource) ? panelSize.Width : WidthSource,
				Height = panelSize.Height
			};

			return !state.Validate() ? Size.Empty : state.MeasureArrange(isMeasure);
		}

	    protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
        {
	        return MeasureArrange(availableSize, context, true);
        }

        protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
        {
	        return MeasureArrange(finalSize, context, false);
        }

        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
	        InvalidateMeasure();
        }
    }
}
