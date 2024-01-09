using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ScrollToSelectedItemBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is { RowSelection: { } rowSelection })
		{
			Observable.FromEventPattern(rowSelection, nameof(rowSelection.SelectionChanged))
				.Select(x =>
				{
					var selectedIndex = rowSelection.SelectedIndex;
					var selectedIndexPath = selectedIndex.FirstOrDefault();
					if (AssociatedObject.Rows is null)
					{
						return selectedIndexPath;
					}

					// Get the actual index in the list of items.
					var rowIndex = AssociatedObject.Rows.ModelIndexToRowIndex(selectedIndexPath);

					// Correct the index wih the index of child item, in the case when the selected item is a child.
					if (selectedIndex.Count > 1)
					{
						// Skip 1 because the first index is the parent.
						// Every other index is the child index.
						rowIndex += selectedIndex.Skip(1).Sum();

						// Need to add 1 to get the correct index.
						rowIndex += 1;
					}

					return rowIndex;
				})
				.WhereNotNull()
				.Do(ScrollToItemIndex)
				.Subscribe()
				.DisposeWith(disposable);
		}
	}

	private void ScrollToItemIndex(int index)
	{
		if (AssociatedObject is { RowsPresenter: { } rowsPresenter })
		{
			Dispatcher.UIThread.Post(() => rowsPresenter.BringIntoView(index));
		}
	}
}
