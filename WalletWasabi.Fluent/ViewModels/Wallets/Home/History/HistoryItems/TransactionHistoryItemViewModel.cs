using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs;

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

		CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable;
		CanCancelTransaction = transactionSummary.Transaction.IsCancelable;

		var canCancelTransaction = this.WhenAnyValue(x => x.IsConfirmed)
			.Select(x => !x)
			.ObserveOn(RxApp.MainThreadScheduler);

		SpeedUpTransactionCommand = ReactiveCommand.Create(
			() =>
			{
				uiContext.Navigate().To().BoostTransactionDialog(new BoostedTransactionPreview(walletVm.Wallet.Synchronizer.UsdExchangeRate)
				{
					Destination = "some destination",
					Amount = Money.FromUnit(1234, MoneyUnit.Satoshi),
					Labels = new LabelsArray("label1", "label2", "label3"),
					Fee = Money.FromUnit(25, MoneyUnit.Satoshi),
					ConfirmationTime = TimeSpan.FromMinutes(20),
				});
			});

		CancelTransactionCommand = ReactiveCommand.Create(
			() =>
			{
				if (transactionSummary.Transaction.WalletOutputs.Any())
				{
					//IF change present THEN make the change the only output
					var change = transactionSummary.Transaction.WalletOutputs.First();
					var originalTransaction = transactionSummary.Transaction.Transaction;
					var cancelTransaction = originalTransaction.Clone();
					//cancelTransaction.Outputs.Clear();
				}
				else
				{
					//ELSE THEN replace the output with a new output that's ours
				}
			},
			canCancelTransaction);

		DateString = Date.ToLocalTime().ToUserFacingString();
	}

	public bool CanCancelTransaction { get; }

	public bool CanSpeedUpTransaction { get; }
}
