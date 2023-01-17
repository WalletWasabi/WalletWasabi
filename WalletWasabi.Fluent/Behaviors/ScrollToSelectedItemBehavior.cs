using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ScrollToSelectedItemBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is { SelectionInteraction: { } selection, RowSelection: { } rowSelection })
		{
			Observable.FromEventPattern(selection, nameof(selection.SelectionChanged))
				.Select(x => rowSelection.SelectedIndex.FirstOrDefault())
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
			rowsPresenter.BringIntoView(index);
		}
	}
}
