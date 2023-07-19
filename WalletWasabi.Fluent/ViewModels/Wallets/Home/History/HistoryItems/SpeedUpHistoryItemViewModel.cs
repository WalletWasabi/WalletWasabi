using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

internal class SpeedUpHistoryItemViewModel : HistoryItemViewModelBase
{
	private readonly IEnumerable<HistoryItemViewModelBase> _children;

	public SpeedUpHistoryItemViewModel(int orderIndex, TransactionSummary transactionSummary, HistoryItemViewModelBase parent, IEnumerable<HistoryItemViewModelBase> children) : base(orderIndex, transactionSummary)
	{
		_children = children.Reverse();
		IsSpeedUp = true;
		IncomingAmount = parent.IncomingAmount;
		OutgoingAmount = parent.OutgoingAmount;
		OrderIndex = parent.OrderIndex;
		Date = parent.Date.ToLocalTime();
		DateString = parent.Date.ToLocalTime().ToUserFacingString();
		Labels = parent.Labels;
		ShowDetailsCommand = parent.ShowDetailsCommand;
	}

	protected override ObservableCollection<HistoryItemViewModelBase> LoadChildren()
	{
		return new ObservableCollection<HistoryItemViewModelBase>(_children);
	}

	public override bool HasChildren()
	{
		return Children.Any();
	}

	public void SetBalance(Money balance)
	{
		Balance = balance;
	}
}
