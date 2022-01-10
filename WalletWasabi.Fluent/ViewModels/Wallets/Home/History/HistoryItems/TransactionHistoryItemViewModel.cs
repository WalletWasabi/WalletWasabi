using System.Linq;
using System.Reactive;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	public TransactionHistoryItemViewModel(
		int orderIndex,
		TransactionSummary transactionSummary,
		WalletViewModel walletViewModel,
		Money balance,
		IObservable<Unit> updateTrigger)
		: base(orderIndex, transactionSummary)
	{
		Label = transactionSummary.Label.Take(1).ToList();
		FilteredLabel = transactionSummary.Label.Skip(1).ToList();
		IsConfirmed = transactionSummary.IsConfirmed();
		Date = transactionSummary.DateTime.ToLocalTime();
		Balance = balance;

		var amount = transactionSummary.Amount;
		if (amount < Money.Zero)
		{
			OutgoingAmount = amount * -1;
		}
		else
		{
			IncomingAmount = amount;
		}

		ShowDetailsCommand = ReactiveCommand.Create(() =>
			RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(
				new TransactionDetailsViewModel(transactionSummary, walletViewModel.Wallet, updateTrigger)));

		DateString = $"{Date.ToLocalTime():MM/dd/yy HH:mm}";
	}
}
