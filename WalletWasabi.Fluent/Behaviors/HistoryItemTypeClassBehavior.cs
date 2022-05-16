using System.Reactive.Disposables;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.Behaviors;

public class HistoryItemTypeClassBehavior : AttachedToVisualTreeBehavior<TreeDataGridRow>
{
	private const string TransactionClass = "Transaction";

	private const string CoinJoinClass = "CoinJoin";

	private const string CoinJoinsClass = "CoinJoins";

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		AssociatedObject?.WhenAnyValue(x => x.DataContext)
			.Subscribe(x =>
			{
				RemoveClasses();
				AddClasses(x);
			})
			.DisposeWith(disposable);
	}

	protected override void OnDetachedFromVisualTree()
	{
		RemoveClasses();
	}

	private void AddClasses(object? dataContext)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		switch (dataContext)
		{
			case TransactionHistoryItemViewModel:
				AssociatedObject.Classes.Add(TransactionClass);
				break;
			case CoinJoinHistoryItemViewModel:
				AssociatedObject.Classes.Add(CoinJoinClass);
				break;
			case CoinJoinsHistoryItemViewModel:
				AssociatedObject.Classes.Add(CoinJoinsClass);
				break;
		}
	}

	private void RemoveClasses()
	{
		if (AssociatedObject is null)
		{
			return;
		}

		if (AssociatedObject.Classes.Contains(TransactionClass))
		{
			AssociatedObject.Classes.Remove(TransactionClass);
		}

		if (AssociatedObject.Classes.Contains(CoinJoinClass))
		{
			AssociatedObject.Classes.Remove(CoinJoinClass);
		}

		if (AssociatedObject.Classes.Contains(CoinJoinsClass))
		{
			AssociatedObject.Classes.Remove(CoinJoinsClass);
		}
	}
}