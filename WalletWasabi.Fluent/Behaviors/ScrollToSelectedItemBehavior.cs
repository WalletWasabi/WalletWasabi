using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ScrollToSelectedItemBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is { SelectionInteraction: { } selection, RowSelection: { } rowSelection })
		{
			Observable.FromEventPattern(selection, nameof(selection.SelectionChanged))
				.Select(_ => rowSelection.SelectedIndex)
				.WhereNotNull()
				.Do(ScrollToItemIndex)
				.Subscribe()
				.DisposeWith(disposable);
		}
	}

	private void ScrollToItemIndex(IndexPath index)
	{
		if (AssociatedObject is { RowsPresenter: { Items: { } } rowsPresenter } )
		{
			var toSelect = index.Sum();
			if (rowsPresenter.Items != null)
			{
				var finalIndex = Math.Min(toSelect + 2, rowsPresenter.Items.Count);
				rowsPresenter.BringIntoView(finalIndex);
			}
		}
	}
}
