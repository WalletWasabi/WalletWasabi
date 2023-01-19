using System.Reactive.Linq;
using CommunityToolkit.Mvvm.Input;
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

		ShowDetailsCommand = new RelayCommand(() =>
			RoutableViewModel.Navigate(NavigationTarget.DialogScreen).To(
				new TransactionDetailsViewModel(transactionSummary, walletVm)));

		SpeedUpTransactionCommand = new RelayCommand(
			() =>
			{
				// TODO: Show speed up transaction dialog.
			},
			canExecute: () => !IsConfirmed);

		// this.WhenAnyValue(x => x.IsConfirmed)
		// 	.Subscribe(_ => SpeedUpTransactionCommand.NotifyCanExecuteChanged()); // TODO RelayCommand: NRE with normal way

		DateString = $"{Date.ToLocalTime():MM/dd/yyyy HH:mm}";
	}
}
