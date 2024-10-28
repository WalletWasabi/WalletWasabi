using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;

[NavigationMetaData(NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CancelTransactionDialogViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	private readonly CancellingTransaction _cancellingTransaction;

	private CancelTransactionDialogViewModel(IWalletModel wallet, CancellingTransaction cancellingTransaction)
	{
		Title = Lang.Resources.CancelTransactionDialogViewModel_Title;

		_wallet = wallet;
		_cancellingTransaction = cancellingTransaction;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		Fee = cancellingTransaction.Fee;

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnCancelTransactionAsync(cancellingTransaction));
	}

	public Amount Fee { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		// Close dialog if target transaction is already confirmed.
		_wallet.Transactions.Cache
			.Watch(_cancellingTransaction.TargetTransaction.Id)
			.Where(change => change.Current.IsConfirmed)
			.Do(_ => Navigate().Back())
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task OnCancelTransactionAsync(CancellingTransaction cancellingTransaction)
	{
		IsBusy = true;

		try
		{
			var isAuthorized = await AuthorizeForPasswordAsync();
			if (isAuthorized)
			{
				await _wallet.Transactions.SendAsync(cancellingTransaction);
				var (title, caption) = ( Lang.Resources.Words_Success,  Lang.Resources.CancelTransactionDialogViewModel_Success);

				// TODO: Remove this after SendSuccessViewModel is decoupled
				var wallet = MainViewModel.Instance.NavBar.Wallets.First(x => x.Wallet.WalletName == _wallet.Name).Wallet;

				UiContext.Navigate().To().SendSuccess(cancellingTransaction.CancelTransaction.Transaction, title, caption, NavigationTarget.CompactDialogScreen);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			var msg = cancellingTransaction.TargetTransaction.IsConfirmed ?  Lang.Resources.CancelTransactionDialogViewModel_Error_AlreadyConfirmed_Message : ex.ToUserFriendlyString();
			UiContext.Navigate().To().ShowErrorDialog(
				msg,
				Lang.Resources.CancelTransactionDialogViewModel_Error_AlreadyConfirmed_Title,
				Lang.Resources.CancelTransactionDialogViewModel_Error_AlreadyConfirmed_Caption,
				NavigationTarget.CompactDialogScreen);
		}

		IsBusy = false;
	}

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (_wallet.Auth.HasPassword)
		{
			return await Navigate().To().PasswordAuthDialog(_wallet,  Lang.Resources.Words_Send).GetResultAsync();
		}

		return true;
	}
}
