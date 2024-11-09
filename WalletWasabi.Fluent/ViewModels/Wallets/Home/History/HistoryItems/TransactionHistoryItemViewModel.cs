using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

public partial class TransactionHistoryItemViewModel : HistoryItemViewModelBase
{
	private IWalletModel _wallet;

	private TransactionHistoryItemViewModel(IWalletModel wallet, TransactionModel transaction) : base(transaction)
	{
		_wallet = wallet;

		CanBeSpedUp = transaction.CanSpeedUpTransaction && !IsChild;
		ShowDetailsCommand = ReactiveCommand.Create(() => UiContext.Navigate().To().TransactionDetails(wallet, transaction));
		SpeedUpTransactionCommand = ReactiveCommand.CreateFromTask(async () => await OnSpeedUpTransactionAsync(transaction, CancellationToken.None), Observable.Return(CanBeSpedUp));
		CancelTransactionCommand = ReactiveCommand.Create(() => OnCancelTransaction(transaction), Observable.Return(transaction.CanCancelTransaction));
		HasBeenSpedUp = transaction.HasBeenSpedUp;
	}

	public bool TransactionOperationsVisible => Transaction.CanCancelTransaction || CanBeSpedUp;

	private async Task OnSpeedUpTransactionAsync(TransactionModel transaction, CancellationToken cancellationToken)
	{
		try
		{
			var speedupTransaction = await _wallet.Transactions.CreateSpeedUpTransactionAsync(transaction, cancellationToken);
			UiContext.Navigate().To().SpeedUpTransactionDialog(_wallet, speedupTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), Lang.Resources.TransactionHistoryItemViewModel_Error_SpeedUp_Title, Lang.Resources.TransactionHistoryItemViewModel_Error_SpeedUp_Caption);
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
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), Lang.Resources.TransactionHistoryItemViewModel_Error_Cancel_Title, Lang.Resources.TransactionHistoryItemViewModel_Error_Cancel_Caption);
		}
	}
}
