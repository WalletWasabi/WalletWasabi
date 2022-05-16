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

		AssociatedObject.LayoutUpdated += AssociatedObjectOnLayoutUpdated;
	}

	protected override void OnDetachedFromVisualTree()
	{
		if (AssociatedObject is null)
		{
			return;
		}

		AssociatedObject.LayoutUpdated -= AssociatedObjectOnLayoutUpdated;
	}

	private void AssociatedObjectOnLayoutUpdated(object? sender, EventArgs e)
	{
		if (AssociatedObject is { } presenter)
		{
			foreach (var child in ((IPanel)AssociatedObject).Children)
			{
				if (child is { })
				{
					InvalidateSeparator(child, presenter);
				}
			}
		}
	}

	private void InvalidateSeparator(IControl control, TreeDataGridRowsPresenter presenter)
	{
		if (control.DataContext is not HistoryItemViewModelBase currentHistoryItem)
		{
			return;
		}

		var className = "separator";

		if (currentHistoryItem.IsConfirmed)
		{
			if (control.Classes.Contains(className))
			{
				control.Classes.Set(className, false);
			}
		}
		else
		{
			var index = presenter.GetChildIndex(control);
			if (presenter.Items is { } items
			    && items.Count > index + 1
			    && presenter.Items[index + 1].Model is HistoryItemViewModelBase { IsConfirmed: true })
			{
				control.Classes.Set(className, true);
			}
			else
			{
				if (control.Classes.Contains(className))
				{
					control.Classes.Set(className, false);
				}
			}
		}
	}
}