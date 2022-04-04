using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public class CoinJoinsHistoryItemViewModel : HistoryItemViewModelBase
{
	public CoinJoinsHistoryItemViewModel(int orderIndex, TransactionSummary firstItem)
		: base(orderIndex, firstItem)
	{
		CoinJoinTransactions = new List<TransactionSummary>();
		Label = "Coinjoins";
		FilteredLabel = new List<string>();
		IsCoinJoin = true;

		ShowDetailsCommand = ReactiveCommand.Create(() => RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(new CoinJoinDetailsViewModel(this)));

		Add(firstItem);
	}

	public List<TransactionSummary> CoinJoinTransactions { get; private set; }

	public void Add(TransactionSummary item)
	{
		if (!item.IsOwnCoinjoin)
		{
			throw new InvalidOperationException("Not a coinjoin item!");
		}

		CoinJoinTransactions.Insert(0, item);
		Refresh();
	}

	private void Refresh()
	{
		IsConfirmed = CoinJoinTransactions.All(x => x.IsConfirmed());
		Date = CoinJoinTransactions.Select(tx => tx.DateTime).Max().ToLocalTime();
		OutgoingAmount = CoinJoinTransactions.Sum(x => x.Amount) * -1;
		UpdateDateString();
	}

	protected void UpdateDateString()
	{
		var dates = CoinJoinTransactions.Select(tx => tx.DateTime).ToImmutableArray();
		var firstDate = dates.Min().ToLocalTime();
		var lastDate = dates.Max().ToLocalTime();

		DateString = firstDate.Day == lastDate.Day
			? $"{firstDate:MM/dd/yy}"
			: $"{firstDate:MM/dd/yy} - {lastDate:MM/dd/yy}";
	}

	public void SetBalance(Money balance)
	{
		Balance = balance;
	}
}
