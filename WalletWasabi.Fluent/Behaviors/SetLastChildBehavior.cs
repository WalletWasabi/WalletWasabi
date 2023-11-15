using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.Behaviors;

public class SetLastChildBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is { Rows: { } rows })
		{
			Observable.FromEventPattern(rows, nameof(rows.CollectionChanged))
				.Select(_ => AssociatedObject.RowsPresenter?.Items)
				.WhereNotNull()
				.Do(items =>
				{
					var castedList = items.Cast<HierarchicalRow<HistoryItemViewModelBase>>().ToArray();

					for (var i = 0; i < castedList.Length; i++)
					{
						var currentItem = castedList[i].Model;
						var nextItem = i + 1 < castedList.Length ? castedList[i + 1].Model : null;

						if (currentItem.Transaction.IsChild)
						{
							currentItem.Transaction.IsLastChild = nextItem is null or { Transaction.IsChild: false };
						}
					}
				})
				.Subscribe()
				.DisposeWith(disposable);
		}
	}
}
