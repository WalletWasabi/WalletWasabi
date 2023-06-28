using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	public TransactionHistoryItemViewModel(
		UiContext uiContext,
		int orderIndex,
		TransactionSummary transactionSummary,
		WalletViewModel walletVm,
		Money balance)
		: base(orderIndex, transactionSummary)
	{
		Labels = transactionSummary.Labels;
		IsConfirmed = transactionSummary.IsConfirmed();
		Date = transactionSummary.DateTime.ToLocalTime();
		Balance = balance;

		var confirmations = transactionSummary.GetConfirmations();
		ConfirmedToolTip = $"{confirmations} confirmation{TextHelpers.AddSIfPlural(confirmations)}";

		SetAmount(transactionSummary.Amount, transactionSummary.Fee);

		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(transactionSummary, walletVm));

		var canBoostTransaction = this.WhenAnyValue(x => x.IsConfirmed)
			.Select(x => !x)
			.ObserveOn(RxApp.MainThreadScheduler);

		var canCancelTransaction = this.WhenAnyValue(x => x.IsConfirmed)
			.Select(x => !x)
			.ObserveOn(RxApp.MainThreadScheduler);

		BoostTransactionCommand = ReactiveCommand.Create(
			() =>
			{
				uiContext.Navigate().To().BoostTransactionDialog();
			});

		CancelTransactionCommand = ReactiveCommand.Create(
			() =>
			{
				// TODO: Do whatever to cancel transaction.
			},
			canCancelTransaction);

		DateString = Date.ToLocalTime().ToUserFacingString();
	}
}
