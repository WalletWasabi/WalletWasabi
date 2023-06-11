using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Controls;

public class LabelsItemsPresenter : ItemsPresenter, IStyleable
{
	public static readonly StyledProperty<IBrush> ForegroundProperty =
		AvaloniaProperty.Register<LabelsItemsPresenter, IBrush>("Foreground");

	public static readonly StyledProperty<IBrush> BorderBrushProperty =
		AvaloniaProperty.Register<LabelsItemsPresenter, IBrush>("BorderBrush");

	public static readonly StyledProperty<double> MaxLabelWidthProperty =
		AvaloniaProperty.Register<LabelsItemsPresenter, double>("MaxLabelWidth");

	public double MaxLabelWidth
	{
		get => GetValue(MaxLabelWidthProperty);
		set => SetValue(MaxLabelWidthProperty, value);
	}

	public IBrush Foreground
	{
		get => GetValue(ForegroundProperty);
		set => SetValue(ForegroundProperty, value);
	}

	public IBrush BorderBrush
	{
		get => GetValue(BorderBrushProperty);
		set => SetValue(BorderBrushProperty, value);
	}

	Type IStyleable.StyleKey => typeof(LabelsItemsPresenter);

	protected override void PanelCreated(IPanel panel)
	{
		base.PanelCreated(panel);

		if (panel is LabelsPanel labelsPanel)
		{
			labelsPanel.Presenter = this;
		}
	}
}
