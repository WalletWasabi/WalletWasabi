using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	private IWalletModel _wallet;

	// TODO: Remove the WalletViewModel parameter
	private TransactionHistoryItemViewModel(IWalletModel wallet, TransactionModel transaction, WalletViewModel walletVm) : base(transaction)
	{
		_wallet = wallet;
		WalletVm = walletVm;

		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(walletVm.WalletModel, transaction.TransactionSummary));
		SpeedUpTransactionCommand = ReactiveCommand.Create(() => OnSpeedUpTransaction(transaction), Observable.Return(transaction.CanSpeedUpTransaction));
		CancelTransactionCommand = ReactiveCommand.Create(() => OnCancelTransaction(transaction), Observable.Return(transaction.CanCancelTransaction));
	}

	public bool TransactionOperationsVisible => Transaction.CanCancelTransaction || Transaction.CanSpeedUpTransaction;

	public WalletViewModel WalletVm { get; }
	public Wallet Wallet => WalletVm.Wallet;
	public KeyManager KeyManager => Wallet.KeyManager;

	private void OnSpeedUpTransaction(TransactionModel transaction)
	{
		try
		{
			var (transactionToSpeedUp, boostingTransaction) = _wallet.Transactions.CreateSpeedUpTransaction(transaction);
			UiContext.Navigate().To().SpeedUpTransactionDialog(WalletVm.UiTriggers, WalletVm.Wallet, transactionToSpeedUp, boostingTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), "Speed Up failed", "Wasabi could not initiate the transaction speed up process.");
		}
	}

	private void OnCancelTransaction(TransactionModel transaction)
	{
		try
		{
			var cancellingTransaction = _wallet.Transactions.CreateCancellingTransaction(transaction);
			UiContext.Navigate().To().CancelTransactionDialog(WalletVm.UiTriggers, Wallet, transaction.TransactionSummary.Transaction, cancellingTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), "Cancel failed", "Wasabi could not initiate the cancelling process.");
		}
	}
}
