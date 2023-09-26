using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;

[NavigationMetaData(Title = "Cancel Transaction", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CancelTransactionDialogViewModel : RoutableViewModel
{
	private readonly UiTriggers _triggers;
	private readonly Wallet _wallet;
	private readonly SmartTransaction _transactionToCancel;

	public CancelTransactionDialogViewModel(UiContext uiContext, UiTriggers triggers, Wallet wallet, SmartTransaction transactionToCancel, BuildTransactionResult cancellingTransaction)
	{
		UiContext = uiContext;
		_triggers = triggers;
		_wallet = wallet;
		_transactionToCancel = transactionToCancel;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		var cancelFee = cancellingTransaction.Fee;
		Fee = uiContext.CreateAmount(cancelFee);

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnCancelTransactionAsync(cancellingTransaction));
	}

	public BtcAmount Fee { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_triggers.TransactionsUpdateTrigger
			.Select(x =>
			{
				_ = _wallet.TryGetTransaction(_transactionToCancel.GetHash(), out SmartTransaction? tx);
				return tx;
			})
			.WhereNotNull()
			.Where(s => s.Confirmed)
			.Do(_ => Navigate().Back())
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task OnCancelTransactionAsync(BuildTransactionResult cancellingTransaction)
	{
		IsBusy = true;

		try
		{
			var isAuthorized = await AuthorizeForPasswordAsync();
			if (isAuthorized)
			{
				await Services.TransactionBroadcaster.SendTransactionAsync(cancellingTransaction.Transaction);
				_wallet.UpdateUsedHdPubKeysLabels(cancellingTransaction.HdPubKeysWithNewLabels);
				var (title, caption) = ("Success", "Your transaction has been successfully cancelled.");
				UiContext.Navigate().To().SendSuccess(_wallet, cancellingTransaction.Transaction, title, caption, NavigationTarget.CompactDialogScreen);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);

			var msg = _transactionToCancel.Confirmed ? "The transaction is already confirmed." : ex.ToUserFriendlyString();

			UiContext.Navigate().To().ShowErrorDialog(msg, "Cancellation Failed", "Wasabi was unable to cancel your transaction.", NavigationTarget.CompactDialogScreen);
		}

		IsBusy = false;
	}

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (!string.IsNullOrEmpty(_wallet.Kitchen.SaltSoup()))
		{
			var result = UiContext.Navigate().To().PasswordAuthDialog(new WalletModel(_wallet));
			var dialogResult = await result.GetResultAsync();
			return dialogResult;
		}

		return true;
	}
}
