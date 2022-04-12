using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using ReactiveUI;

namespace WalletWasabi.Fluent.Controls;

public class LabelsItemsPresenter : ItemsPresenter, IStyleable
{
	Type IStyleable.StyleKey => typeof(ItemsPresenter);

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
}
