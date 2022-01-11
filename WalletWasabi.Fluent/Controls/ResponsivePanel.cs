using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

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

	public static readonly AttachedProperty<AvaloniaList<int>?> ColumnSpanProperty =
		AvaloniaProperty.RegisterAttached<ResponsivePanel, Control, AvaloniaList<int>?>("ColumnSpan");

	public static readonly AttachedProperty<AvaloniaList<int>?> RowSpanProperty =
		AvaloniaProperty.RegisterAttached<ResponsivePanel, Control, AvaloniaList<int>?>("RowSpan");

	public static AvaloniaList<int>? GetColumnSpan(Control? element)
	{
		Contract.Requires<ArgumentNullException>(element is not null);
		return element!.GetValue(ColumnSpanProperty);
	}

	public static void SetColumnSpan(Control? element, AvaloniaList<int>? value)
	{
		Contract.Requires<ArgumentNullException>(element is not null);
		element!.SetValue(ColumnSpanProperty, value);
	}

	public static AvaloniaList<int>? GetRowSpan(Control? element)
	{
		Contract.Requires<ArgumentNullException>(element is not null);
		return element!.GetValue(RowSpanProperty);
	}

	public static void SetRowSpan(Control? element, AvaloniaList<int>? value)
	{
		Contract.Requires<ArgumentNullException>(element is not null);
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

	private Size MeasureArrange(Size panelSize, bool isMeasure)
	{
		var state = new ResponsivePanelState(Children)
		{
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

	protected override Size MeasureOverride(Size availableSize)
	{
		return MeasureArrange(availableSize, true);
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		return MeasureArrange(finalSize, false);
	}
}
