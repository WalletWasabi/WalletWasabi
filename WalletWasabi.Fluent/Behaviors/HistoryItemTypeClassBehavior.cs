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
				switch (x)
				{
					case TransactionHistoryItemViewModel:
						AssociatedObject.Classes.Add("Transaction");
						break;
					case CoinJoinHistoryItemViewModel:
						AssociatedObject.Classes.Add("CoinJoin");
						break;
					case CoinJoinsHistoryItemViewModel:
						AssociatedObject.Classes.Add("CoinJoins");
						break;
				}
			})
			.DisposeWith(disposable);
	}
}