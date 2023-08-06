using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Controls;

public class LabelsItemsPresenter : ItemsControl
{
	public static readonly StyledProperty<double> MaxLabelWidthProperty =
		AvaloniaProperty.Register<LabelsItemsPresenter, double>("MaxLabelWidth");

	public double MaxLabelWidth
	{
		get => GetValue(MaxLabelWidthProperty);
		set => SetValue(MaxLabelWidthProperty, value);
	}

	protected override Type StyleKeyOverride => typeof(LabelsItemsPresenter);
}
