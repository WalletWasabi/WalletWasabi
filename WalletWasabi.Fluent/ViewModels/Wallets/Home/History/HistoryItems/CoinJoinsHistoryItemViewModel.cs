using System.Collections.Generic;
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
		Label = new List<string> { "Privacy Boost" };
		FilteredLabel = new List<string>();
		IsCoinJoin = true;

		ShowDetailsCommand = ReactiveCommand.Create(() =>
			RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(new CoinJoinDetailsViewModel(this)));

		Add(firstItem);
	}

	public List<TransactionSummary> CoinJoinTransactions { get; private set; }

	public void Add(TransactionSummary item)
	{
		if (!item.IsLikelyCoinJoinOutput)
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
		UpdateAmount();
		UpdateDateString();
	}

	public override void Update(HistoryItemViewModelBase item)
	{
		if (item is not CoinJoinsHistoryItemViewModel coinJoinHistoryItemViewModel)
		{
			throw new InvalidOperationException("Not the same type!");
		}

		CoinJoinTransactions = coinJoinHistoryItemViewModel.CoinJoinTransactions;
		UpdateAmount();

		base.Update(item);

		this.RaisePropertyChanged();
	}

	protected void UpdateDateString()
	{
		var dates = CoinJoinTransactions.Select(tx => tx.DateTime);
		var firstDate = dates.Min().ToLocalTime();
		var lastDate = dates.Max().ToLocalTime();

		DateString = firstDate.Day == lastDate.Day
			? $"{firstDate:MM/dd/yy}"
			: $"{firstDate:MM/dd/yy} - {lastDate:MM/dd/yy}";
	}

	private void UpdateAmount()
	{
		OutgoingAmount = CoinJoinTransactions.Sum(x => x.Amount) * -1;
	}

	public void SetBalance(Money balance)
	{
		Balance = balance;
	}
}
