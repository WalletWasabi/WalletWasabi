using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;
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
		Date = transactionSummary.DateTime.ToLocalTime();
		Balance = balance;
		WalletVm = walletVm;

		IsCancellation = transactionSummary.IsCancellation;
		IsSpeedUp = transactionSummary.IsSpeedUp;
		IsCPFP = transactionSummary.IsCPFP;
		IsCPFPd = transactionSummary.IsCPFPd;

		SetAmount(transactionSummary.Amount, transactionSummary.Fee);

		DateString = Date.ToLocalTime().ToUserFacingString();

		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(transactionSummary, walletVm));
		CanCancelTransaction = transactionSummary.Transaction.IsCancellable(KeyManager);
		CanSpeedUpTransaction = transactionSummary.Transaction.IsSpeedupable(KeyManager);
		SpeedUpTransactionCommand = ReactiveCommand.Create(() => OnSpeedUpTransaction(transactionSummary.Transaction), Observable.Return(CanSpeedUpTransaction));
		CancelTransactionCommand = ReactiveCommand.Create(() => OnCancelTransaction(transactionSummary.Transaction), Observable.Return(CanCancelTransaction));
	}

	public bool CanCancelTransaction { get; }
	public bool CanSpeedUpTransaction { get; }
	public bool TransactionOperationsVisible => CanCancelTransaction || CanSpeedUpTransaction;

	public WalletViewModel WalletVm { get; }
	public Wallet Wallet => WalletVm.Wallet;
	public KeyManager KeyManager => Wallet.KeyManager;

	private void OnSpeedUpTransaction(SmartTransaction transactionToSpeedUp)
	{
		try
		{
			var boostingTransaction = Wallet.SpeedUpTransaction(transactionToSpeedUp);
			UiContext.Navigate().To().SpeedUpTransactionDialog(WalletVm.Wallet, transactionToSpeedUp, boostingTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), "Speed Up failed", "Wasabi could not initiate the transaction speed up process.");
		}
	}

	private void OnCancelTransaction(SmartTransaction transactionToCancel)
	{
		try
		{
			var cancellingTransaction = Wallet.CancelTransaction(transactionToCancel);
			UiContext.Navigate().To().CancelTransactionDialog(Wallet, transactionToCancel, cancellingTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), "Cancel failed", "Wasabi could not initiate the cancelling process.");
		}
	}
}
