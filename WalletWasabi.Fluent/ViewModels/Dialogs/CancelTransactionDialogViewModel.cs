using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Cancel Transaction")]
public partial class CancelTransactionDialogViewModel : DialogViewModelBase<Unit>
{
	private readonly Wallet _wallet;

	private CancelTransactionDialogViewModel(Wallet wallet, SmartTransaction original, SmartTransaction cancellingTransaction)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		var originalFee = original.WalletInputs.Sum(x => x.Amount) - original.OutputValues.Sum(x => x);
		var cancelFeel = cancellingTransaction.WalletInputs.Sum(x => x.Amount) - cancellingTransaction.OutputValues.Sum(x => x);
		FeeDifference = cancelFeel - originalFee;

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnCancelTransaction(cancellingTransaction));
	}

	public Money FeeDifference { get; }

	private async Task OnCancelTransaction(SmartTransaction cancelTransaction)
	{
		IsBusy = true;

		try
		{
			var isAuthorized = await AuthorizeForPasswordAsync();
			if (isAuthorized)
			{
				await Services.TransactionBroadcaster.SendTransactionAsync(cancelTransaction);
				UiContext.Navigate().To().SendSuccess(_wallet, cancelTransaction);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), "Speed Up failed", "Wasabi was unable to cancel your transaction.");
		}

		IsBusy = false;
	}

	protected override void OnDialogClosed()
	{
	}

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
}
