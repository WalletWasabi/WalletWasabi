using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	public TransactionHistoryItemViewModel(UiContext uiContext, TransactionModel transaction, WalletViewModel walletVm) : base(uiContext, transaction)
	{
		WalletVm = walletVm;
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
			// If the transaction has CPFPs, then we want to speed them up instead of us.
			// Although this does happen inside the SpeedUpTransaction method, but we want to give the tx that was actually sped up to SpeedUpTransactionDialog.
			if (transactionToSpeedUp.TryGetLargestCPFP(WalletVm.Wallet.KeyManager, out var largestCpfp))
			{
				transactionToSpeedUp = largestCpfp;
			}
			var boostingTransaction = Wallet.SpeedUpTransaction(transactionToSpeedUp);
			UiContext.Navigate().To().SpeedUpTransactionDialog(WalletVm.UiTriggers, WalletVm.Wallet, transactionToSpeedUp, boostingTransaction);
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
			UiContext.Navigate().To().CancelTransactionDialog(WalletVm.UiTriggers, Wallet, transactionToCancel, cancellingTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), "Cancel failed", "Wasabi could not initiate the cancelling process.");
		}
	}
}
