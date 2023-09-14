using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

internal class SpeedUpHistoryItemViewModel : HistoryItemViewModelBase
{
	private readonly IEnumerable<HistoryItemViewModelBase> _children;

	public SpeedUpHistoryItemViewModel(
		int orderIndex,
		SmartTransaction transaction,
		WalletViewModel walletVm,
		HistoryItemViewModelBase parent,
		IEnumerable<HistoryItemViewModelBase> children)
		: base(orderIndex, transaction)
	{
		_children = children.Reverse();
		IsConfirmed = children.All(x => x.IsConfirmed);
		IsSpeedUp = true;
		IncomingAmount = parent.IncomingAmount;
		OutgoingAmount = parent.OutgoingAmount;
		OrderIndex = parent.OrderIndex;
		Date = parent.Date.ToLocalTime();
		DateString = parent.Date.ToLocalTime().ToUserFacingString();
		Labels = parent.Labels;
		ShowDetailsCommand = parent.ShowDetailsCommand;

		foreach (var child in _children)
		{
			child.IsChild = true;
		}

		CanCancelTransaction = transaction.IsCancellable(walletVm.Wallet.KeyManager);
		CanSpeedUpTransaction = transaction.IsSpeedupable(walletVm.Wallet.KeyManager);
		SpeedUpTransactionCommand = parent.SpeedUpTransactionCommand;
		CancelTransactionCommand = parent.CancelTransactionCommand;
	}

	public bool CanSpeedUpTransaction { get; }

	public bool CanCancelTransaction { get; }

	public bool TransactionOperationsVisible => CanCancelTransaction || CanSpeedUpTransaction;

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
