using System.Reactive.Disposables;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.Behaviors;

public class HistoryItemTypeClassBehavior : AttachedToVisualTreeBehavior<TreeDataGridRow>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		AssociatedObject?.WhenAnyValue(x => x.DataContext)
			.Subscribe(x =>
			{
				if (x is TransactionHistoryItemViewModel)
				{
					AssociatedObject.Classes.Add("Transaction");
				}
				else if (x is CoinJoinHistoryItemViewModel)
				{
					AssociatedObject.Classes.Add("CoinJoin");
				}
				else if (x is CoinJoinsHistoryItemViewModel)
				{
					AssociatedObject.Classes.Add("CoinJoins");
				}
			})
			.DisposeWith(disposable);
	}
}