using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;

[NavigationMetaData(Title = "Speed Up Transaction", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class SpeedUpTransactionDialogViewModel : RoutableViewModel
{
	private readonly SpeedupTransaction _speedupTransaction;
	private readonly IWalletModel _wallet;

	private SpeedUpTransactionDialogViewModel(IWalletModel wallet, SpeedupTransaction speedupTransaction)
	{
		_wallet = wallet;
		_speedupTransaction = speedupTransaction;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnSpeedUpTransactionAsync(speedupTransaction));

		Fee = speedupTransaction.Fee;
		AreWePayingTheFee = speedupTransaction.AreWePayingTheFee;
	}

	public Amount Fee { get; }

	public bool AreWePayingTheFee { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		// Close dialog if target transaction is already confirmed.
		_wallet.Transactions.List
							.ToObservableChangeSet(x => x.Id)
							.ToCollection()
							.Select(col => col.FirstOrDefault(x => x.Id == _speedupTransaction.TargetTransaction.GetHash()))
							.WhereNotNull()
							.Where(s => s.IsConfirmed)
							.Do(_ => Navigate().Back())
							.Subscribe()
							.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task OnSpeedUpTransactionAsync(SpeedupTransaction speedupTransaction)
	{
		IsBusy = true;

		try
		{
			var isAuthorized = await AuthorizeForPasswordAsync();
			if (isAuthorized)
			{
				await _wallet.Transactions.SendAsync(speedupTransaction);
				var (title, caption) = ("Success", "Your transaction has been successfully accelerated.");

				// TODO: Remove this after SendSuccessViewModel is decoupled
				var wallet = MainViewModel.Instance.NavBar.Wallets.First(x => x.Wallet.WalletName == _wallet.Name).Wallet;

				UiContext.Navigate().To().SendSuccess(wallet, speedupTransaction.BoostingTransaction.Transaction, title, caption, NavigationTarget.CompactDialogScreen);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			var msg = speedupTransaction.TargetTransaction.Confirmed ? "The transaction is already confirmed." : ex.ToUserFriendlyString();
			UiContext.Navigate().To().ShowErrorDialog(msg, "Speed Up Failed", "Wasabi was unable to speed up your transaction.", NavigationTarget.CompactDialogScreen);
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
