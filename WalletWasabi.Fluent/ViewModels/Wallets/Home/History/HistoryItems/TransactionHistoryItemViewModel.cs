using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
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
		WalletVm = walletVm;

		var confirmations = transactionSummary.GetConfirmations();
		ConfirmedToolTip = $"{confirmations} confirmation{TextHelpers.AddSIfPlural(confirmations)}";

		SetAmount(transactionSummary.Amount, transactionSummary.Fee);

		DateString = Date.ToLocalTime().ToUserFacingString();

		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(transactionSummary, walletVm));
		CanCancelTransaction = transactionSummary.Transaction.IsCancelable(KeyManager);
		CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable(KeyManager);
		SpeedUpTransactionCommand = ReactiveCommand.Create(() => OnSpeedUpTransaction(transactionSummary.Transaction), Observable.Return(CanSpeedUpTransaction));
		CancelTransactionCommand = ReactiveCommand.Create(() => OnCancelTransaction(transactionSummary), Observable.Return(CanCancelTransaction));
	}

	public bool CanCancelTransaction { get; }

	public bool CanSpeedUpTransaction { get; }
	public WalletViewModel WalletVm { get; }
	public Wallet Wallet => WalletVm.Wallet;
	public KeyManager KeyManager => Wallet.KeyManager;

	private void OnSpeedUpTransaction(SmartTransaction transactionToSpeedUp)
	{
		var speedUpTransaction = TransactionSpeedUpHelper.CreateSpeedUpTransaction(transactionToSpeedUp, Wallet);

		UiContext.Navigate().To().BoostTransactionDialog(
			new BoostedTransactionPreview(Wallet.Synchronizer.UsdExchangeRate)
			{
				Destination = "some destination",
				Amount = Money.FromUnit(1234, MoneyUnit.Satoshi),
				Labels = new LabelsArray("label1", "label2", "label3"),
				Fee = Money.FromUnit(25, MoneyUnit.Satoshi),
				ConfirmationTime = TimeSpan.FromMinutes(20)
			});
	}

	private void OnCancelTransaction(TransactionSummary transactionSummary)
	{
		var cancellingTransaction = TransactionCancellationHelper.CreateCancellation(transactionSummary.Transaction, Wallet);
		UiContext.Navigate().To().CancelTransactionDialog(Wallet, transactionSummary.Transaction, cancellingTransaction);
	}
}
