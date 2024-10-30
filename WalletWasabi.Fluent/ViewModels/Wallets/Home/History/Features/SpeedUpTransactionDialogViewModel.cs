using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;

[NavigationMetaData(Title = "SpeedUpTransactionDialogViewModel_Title", NavigationTarget = NavigationTarget.CompactDialogScreen)]
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
		_wallet.Transactions.Cache
			.Connect()
			.Watch(_speedupTransaction.TargetTransaction.GetHash())
			.Where(change => change.Current.IsConfirmed)
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
				var (title, caption) = (Lang.Resources.Words_Success, Lang.Resources.SpeedUpTransactionDialogViewModel_Success_Caption);

				UiContext.Navigate().To().SendSuccess(speedupTransaction.BoostingTransaction.Transaction, title, caption, NavigationTarget.CompactDialogScreen);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			var msg = speedupTransaction.TargetTransaction.Confirmed ?  Lang.Resources.SpeedUpTransactionDialogViewModel_Error_AlreadyConfirmed_Message : ex.ToUserFriendlyString();
			UiContext.Navigate().To().ShowErrorDialog(
				msg,
				Lang.Resources.SpeedUpTransactionDialogViewModel_Error_Generic_Title,
				Lang.Resources.SpeedUpTransactionDialogViewModel_Error_Generic_Caption,
				NavigationTarget.CompactDialogScreen);
		}

		IsBusy = false;
	}

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (_wallet.Auth.HasPassword)
		{
			return await Navigate().To().PasswordAuthDialog(_wallet, Lang.Resources.Words_Send).GetResultAsync();
		}

		return true;
	}
}
