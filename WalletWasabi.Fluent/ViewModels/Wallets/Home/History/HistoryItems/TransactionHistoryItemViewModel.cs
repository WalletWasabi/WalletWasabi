using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	private TransactionHistoryItemViewModel(
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

		DateString = Date.ToLocalTime().ToUserFacingString();

		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(transactionSummary, walletVm));
		CanCancelTransaction = transactionSummary.Transaction.IsCancelable(walletVm.Wallet.KeyManager);
		CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable;
		SpeedUpTransactionCommand = ReactiveCommand.Create(() => OnSpeedUpTransaction(transactionSummary.Transaction, walletVm.Wallet), Observable.Return(CanSpeedUpTransaction));
		CancelTransactionCommand = ReactiveCommand.Create(() => OnCancelTransaction(transactionSummary, walletVm), Observable.Return(CanCancelTransaction));
	}

	public bool CanCancelTransaction { get; }

	public bool CanSpeedUpTransaction { get; }

	private void OnSpeedUpTransaction(SmartTransaction transactionToSpeedUp, Wallet wallet)
	{
		var speedUpTransaction = TransactionSpeedUpHelper.CreateSpeedUpTransaction(transactionToSpeedUp, wallet);

		UiContext.Navigate().To().BoostTransactionDialog(
			new BoostedTransactionPreview(wallet.Synchronizer.UsdExchangeRate)
			{
				Destination = "some destination",
				Amount = Money.FromUnit(1234, MoneyUnit.Satoshi),
				Labels = new LabelsArray("label1", "label2", "label3"),
				Fee = Money.FromUnit(25, MoneyUnit.Satoshi),
				ConfirmationTime = TimeSpan.FromMinutes(20)
			});
	}

	private void OnCancelTransaction(TransactionSummary transactionSummary, WalletViewModel walletVm)
	{
		var cancellingTransaction = TransactionCancellationHelper.CreateCancellation(transactionSummary.Transaction, walletVm.Wallet);
		UiContext.Navigate().To().CancelTransactionDialog(walletVm.Wallet, transactionSummary.Transaction, cancellingTransaction);
	}
}
