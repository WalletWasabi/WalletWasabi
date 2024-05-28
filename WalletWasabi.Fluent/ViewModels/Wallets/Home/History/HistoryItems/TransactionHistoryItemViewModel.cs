using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	private IWalletModel _wallet;
	private readonly TransactionModel _transaction;

	private TransactionHistoryItemViewModel(IWalletModel wallet, TransactionModel transaction) : base(transaction)
	{
		_wallet = wallet;
		_transaction = transaction;

		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(wallet, transaction));
		SpeedUpTransactionCommand = ReactiveCommand.Create(() => OnSpeedUpTransaction(transaction), Observable.Return(CanBeSpedUp));
		CancelTransactionCommand = ReactiveCommand.Create(() => OnCancelTransaction(transaction), Observable.Return(transaction.CanCancelTransaction));
		HasBeenSpedUp = transaction.HasBeenSpedUp;
	}

	private bool CanBeSpedUp => _transaction.CanSpeedUpTransaction && !IsChild && !_transaction.HasBeenSpedUp;

	public bool TransactionOperationsVisible => Transaction.CanCancelTransaction || CanBeSpedUp;

	private void OnSpeedUpTransaction(TransactionModel transaction)
	{
		try
		{
			var speedupTransaction = _wallet.Transactions.CreateSpeedUpTransaction(transaction);
			UiContext.Navigate().To().SpeedUpTransactionDialog(_wallet, speedupTransaction);
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
			UiContext.Navigate().To().CancelTransactionDialog(_wallet, cancellingTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), "Cancel failed", "Wasabi could not initiate the cancelling process.");
		}
	}
}
