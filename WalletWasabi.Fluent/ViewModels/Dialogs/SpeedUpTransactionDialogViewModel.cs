using System.Reactive;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Speed Up Transaction")]
public partial class SpeedUpTransactionDialogViewModel : DialogViewModelBase<Unit>
{
	private readonly Wallet _wallet;

	private SpeedUpTransactionDialogViewModel(Wallet wallet, SmartTransaction spedUpTransaction, SmartTransaction original)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnSpeedUpTransactionAsync(spedUpTransaction));

		FeeDifference = Money.Zero;
	}

	private async Task OnSpeedUpTransactionAsync(SmartTransaction spedUpTransaction)
	{
		IsBusy = true;

		try
		{
			var isAuthorized = await AuthorizeForPasswordAsync();
			if (isAuthorized)
			{
				await Services.TransactionBroadcaster.SendTransactionAsync(spedUpTransaction);
				UiContext.Navigate().To().SendSuccess(_wallet, spedUpTransaction);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync("Cancellation", ex.ToUserFriendlyString(), "Wasabi was unable to cancel your transaction.");
		}

		IsBusy = false;
	}

	public Money FeeDifference { get; }

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (!string.IsNullOrEmpty(_wallet.Kitchen.SaltSoup()))
		{
			var result = UiContext.Navigate().To().PasswordAuthDialog(_wallet);
			var dialogResult = await result.GetResultAsync();
			return dialogResult;
		}

		return true;
	}

	protected override void OnDialogClosed()
	{
	}
}
