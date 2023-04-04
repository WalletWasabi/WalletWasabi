using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	public TransactionHistoryItemViewModel(
		int orderIndex,
		TransactionSummary transactionSummary,
		WalletViewModel walletVm,
		Money balance)
		: base(orderIndex, transactionSummary)
	{
		Label = transactionSummary.Label;
		IsConfirmed = transactionSummary.IsConfirmed();
		Date = transactionSummary.DateTime.ToLocalTime();
		Balance = balance;

		var confirmations = transactionSummary.GetConfirmations();
		ConfirmedToolTip = $"{confirmations} confirmation{TextHelpers.AddSIfPlural(confirmations)}";

		var amount = transactionSummary.Amount;
		SetAmount(amount);

		ShowDetailsCommand = ReactiveCommand.Create(() =>
			RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(
				new TransactionDetailsViewModel(transactionSummary, walletVm)));

		var speedUpTransactionCommandCanExecute = this.WhenAnyValue(x => x.IsConfirmed)
			.Select(x => !x)
			.ObserveOn(RxApp.MainThreadScheduler);

		SpeedUpTransactionCommand = ReactiveCommand.Create(
			() =>
			{
				// TODO: Show speed up transaction dialog.
			},
			speedUpTransactionCommandCanExecute);

		DateString = $"{Date.ToLocalTime():MM/dd/yyyy HH:mm}";
	}
}
