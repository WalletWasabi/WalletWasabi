using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;

[NavigationMetaData(Title = "Speed Up Transaction", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class SpeedUpTransactionDialogViewModel : RoutableViewModel
{
	private readonly SpeedUpTransactionResult _speedUpTransactionResult;
	private readonly IWalletModel _wallet;

	public SpeedUpTransactionDialogViewModel(UiContext uiContext, IWalletModel wallet, SpeedUpTransactionResult result, bool areWePayingTheFee) : base(uiContext)
	{
		_wallet = wallet;
		_speedUpTransactionResult = result;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnSpeedUpTransactionAsync(result));

		FeeDifference = wallet.AmountProvider.Create(result.GetFeeDifference());
		AreWePayingTheFee = areWePayingTheFee;
		OriginalFeeRate = result.OriginalFeeRate;
		NewFeeRate = result.NewFeeRate;
	}

	/// <summary>Difference between the new transaction fee and the original transaction fee.</summary>
	public Amount FeeDifference { get; }

	public bool AreWePayingTheFee { get; }

	public FeeRate OriginalFeeRate { get; }

	public FeeRate NewFeeRate { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		// Close dialog if target transaction is already confirmed.
		_wallet.Transactions.Cache
			.Connect()
			.Watch(_speedUpTransactionResult.TransactionToSpeedUp.GetHash())
			.Where(change => change.Current.IsConfirmed)
			.Do(_ => Navigate().Back())
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task OnSpeedUpTransactionAsync(SpeedUpTransactionResult result)
	{
		IsBusy = true;

		try
		{
			var isAuthorized = await AuthorizeForPasswordAsync();
			if (isAuthorized)
			{
				await _wallet.Transactions.SendAsync(result.BoostingTransaction);
				var (title, caption) = ("Success", "Your transaction has been successfully accelerated.");

				UiContext.Navigate().To().SendSuccess(result.BoostingTransaction.Transaction, title, caption, NavigationTarget.CompactDialogScreen);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			var msg = result.TransactionToSpeedUp.Confirmed ? "The transaction is already confirmed." : ex.ToUserFriendlyString();
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
