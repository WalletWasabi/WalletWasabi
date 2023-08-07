using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class LabelsItemsPresenter : ItemsControl
{
	public static readonly StyledProperty<bool> InfiniteWidthMeasureProperty =
		AvaloniaProperty.Register<LabelsItemsPresenter, bool>(nameof(InfiniteWidthMeasure));

	public static readonly StyledProperty<double> MaxLabelWidthProperty =
		AvaloniaProperty.Register<LabelsItemsPresenter, double>("MaxLabelWidth");

	public bool InfiniteWidthMeasure
	{
		get => GetValue(InfiniteWidthMeasureProperty);
		set => SetValue(InfiniteWidthMeasureProperty, value);
	}

	public double MaxLabelWidth
	{
		get => GetValue(MaxLabelWidthProperty);
		set => SetValue(MaxLabelWidthProperty, value);
	}

	protected override Type StyleKeyOverride => typeof(LabelsItemsPresenter);
}
