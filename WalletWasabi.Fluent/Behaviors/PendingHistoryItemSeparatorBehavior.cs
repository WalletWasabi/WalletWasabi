using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.Behaviors;

public class PendingHistoryItemSeparatorBehavior : DisposingBehavior<TreeDataGridRowsPresenter>
{
	private const string ClassName = "separator";

	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is { })
		{
			Observable.FromEventPattern(AssociatedObject, nameof(AssociatedObject.LayoutUpdated))
				.Subscribe(x => AssociatedObjectOnLayoutUpdated(x.Sender, EventArgs.Empty))
				.DisposeWith(disposables);
		}
	}

	private void AssociatedObjectOnLayoutUpdated(object? sender, EventArgs e)
	{
		if (AssociatedObject is not { } presenter)
		{
			return;
		}

		var children = AssociatedObject.GetVisualChildren();
		foreach (var child in children)
		{
			if (child is { })
			{
				InvalidateSeparator((Control) child, presenter);
			}
		}
	}

	private void InvalidateSeparator(Control control, TreeDataGridRowsPresenter presenter)
	{
		if (control.DataContext is not HistoryItemViewModelBase currentHistoryItem)
		{
			return;
		}

		if (currentHistoryItem.Transaction.IsConfirmed)
		{
			if (control.Classes.Contains(ClassName))
			{
				control.Classes.Set(ClassName, false);
			}
		}
		else
		{
			if (IsSeparator(presenter, presenter.GetChildIndex(control)))
			{
				control.Classes.Set(ClassName, true);
			}
			else
			{
				if (control.Classes.Contains(ClassName))
				{
					control.Classes.Set(ClassName, false);
				}
			}
		}

		static bool IsSeparator(TreeDataGridRowsPresenter presenter, int index)
		{
			return presenter.Items is { } items
			       && items.Count > index + 1
			       && presenter.Items[index + 1].Model is HistoryItemViewModelBase vm && vm.Transaction.IsConfirmed;
		}
	}
}
