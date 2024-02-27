using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.Behaviors;

public class HistoryItemTypeClassBehavior : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid>
{
	private const string TransactionClass = "Transaction";

	private const string CoinJoinClass = "CoinJoin";

	private const string CoinJoinsClass = "CoinJoins";

	private const string SpeedUpClass = "SpeedUp";

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable
			.FromEventPattern<TreeDataGridRowEventArgs>(
				AssociatedObject,
				nameof(Avalonia.Controls.TreeDataGrid.RowPrepared))
			.Select(x => x.EventArgs.Row)
			.Subscribe(AddClasses)
			.DisposeWith(disposable);

		Observable
			.FromEventPattern<TreeDataGridRowEventArgs>(
				AssociatedObject,
				nameof(Avalonia.Controls.TreeDataGrid.RowClearing))
			.Select(x => x.EventArgs.Row)
			.Subscribe(RemoveClasses)
			.DisposeWith(disposable);
	}

	private void AddClasses(TreeDataGridRow row)
	{
		switch (row.DataContext)
		{
			case TransactionHistoryItemViewModel:
				row.Classes.Add(TransactionClass);
				break;

			case CoinJoinHistoryItemViewModel:
				row.Classes.Add(CoinJoinClass);
				break;

			case CoinJoinsHistoryItemViewModel:
				row.Classes.Add(CoinJoinsClass);
				break;

			case SpeedUpHistoryItemViewModel:
				row.Classes.Add(SpeedUpClass);
				break;
		}
	}

	private void RemoveClasses(TreeDataGridRow row)
	{
		row.Classes.Remove(TransactionClass);
		row.Classes.Remove(CoinJoinClass);
		row.Classes.Remove(CoinJoinsClass);
		row.Classes.Remove(SpeedUpClass);
	}
}
