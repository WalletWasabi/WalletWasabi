using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;

[NavigationMetaData(Title = "Cancel Transaction", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class CancelTransactionDialogViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	private readonly CancellingTransaction _cancellingTransaction;

	private CancelTransactionDialogViewModel(IWalletModel wallet, CancellingTransaction cancellingTransaction)
	{
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
				var (title, caption) = ("Success", "Your transaction has been successfully cancelled.");

				// TODO: Remove this after SendSuccessViewModel is decoupled
				var wallet = MainViewModel.Instance.NavBar.Wallets.First(x => x.Wallet.WalletName == _wallet.Name).Wallet;

				UiContext.Navigate().To().SendSuccess(cancellingTransaction.CancelTransaction.Transaction, title, caption, NavigationTarget.CompactDialogScreen);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			var msg = cancellingTransaction.TargetTransaction.IsConfirmed ? "The transaction is already confirmed." : ex.ToUserFriendlyString();
			UiContext.Navigate().To().ShowErrorDialog(msg, "Cancellation Failed", "Wasabi was unable to cancel your transaction.", NavigationTarget.CompactDialogScreen);
		}

		IsBusy = false;
	}

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (_wallet.Auth.HasPassword)
		{
			return await Navigate().To().PasswordAuthDialog(_wallet, "Send").GetResultAsync();
		}

		return true;
	}
}
