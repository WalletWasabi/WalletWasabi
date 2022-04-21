using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.Behaviors;

public class PendingHistoryItemSeparatorBehavior : AttachedToVisualTreeBehavior<TreeDataGridRowsPresenter>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject.ChildIndexChanged += OnChildIndexChanged;
	}

	protected override void OnDetachedFromVisualTree()
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject.ChildIndexChanged -= OnChildIndexChanged;
	}

	private void OnChildIndexChanged(object? sender, ChildIndexChangedEventArgs e)
	{
		if (AssociatedObject is { } presenter && e.Child is {} currentItem)
		{
			var index = AssociatedObject.GetChildIndex(currentItem);

			if (currentItem is IControl currentControl && currentControl.DataContext is HistoryItemViewModelBase currentHistoryItem)
			{
				if (!currentHistoryItem.IsConfirmed)
				{
					if (presenter.Items is { } items && items.Count > index + 1)
					{
						var nextItem = presenter.Items[index + 1];
						if (nextItem.Model is HistoryItemViewModelBase nextHistoryItem)
						{
							if (nextHistoryItem.IsConfirmed)
							{
								var classes = currentControl.Classes;
								classes.Set("separator", true);
							}
						}
					}
				}
				else
				{
					var classes = currentControl.Classes;
					classes.Set("separator", false);
				}
			}
		}
	}
}