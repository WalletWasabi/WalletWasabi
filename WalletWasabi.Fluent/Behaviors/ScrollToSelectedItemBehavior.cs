using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ScrollToSelectedItemBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is not { SelectionInteraction: { } selection, RowSelection: { } rowSelection })
		{
			return;
		}

		Observable.FromEventPattern(selection, nameof(selection.SelectionChanged))
			.Select(x => rowSelection.SelectedIndex.FirstOrDefault())
			.WhereNotNull()
			.Do(
				index =>
			{
				if (AssociatedObject.RowsPresenter is not null)
				{
					AssociatedObject.RowsPresenter.BringIntoView(index);
				}
			})
			.Subscribe()
			.DisposeWith(disposable);
	}
}
