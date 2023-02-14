using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class LabelsItemsPresenter : ItemsControl, IStyleable
{
	public static readonly StyledProperty<double> MaxLabelWidthProperty =
		AvaloniaProperty.Register<LabelsItemsPresenter, double>("MaxLabelWidth");

	public double MaxLabelWidth
	{
		get => GetValue(MaxLabelWidthProperty);
		set => SetValue(MaxLabelWidthProperty, value);
	}

	Type IStyleable.StyleKey => typeof(LabelsItemsPresenter);

	// TODO: Breaking change PanelCreated ?
	/*
	protected override void PanelCreated(IPanel panel)
	{
		base.PanelCreated(panel);

		if (panel is LabelsPanel labelsPanel)
		{
			labelsPanel.WhenAnyValue(x => x.VisibleItemsCount)
				.Subscribe(x =>
				{
					if (Items is IEnumerable<string> items)
					{
						labelsPanel.FilteredItems = items.Skip(x).ToList();
					}
					else
					{
						labelsPanel.FilteredItems = new List<string>();
					}
				});
		}
	}
	*/
}
