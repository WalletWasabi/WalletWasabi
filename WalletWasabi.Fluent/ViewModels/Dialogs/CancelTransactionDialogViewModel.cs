using System.Linq;
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

[NavigationMetaData(Title = "Cancel transaction")]
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

	private async Task OnCancelTransaction(SmartTransaction cancelTransaction)
	{
		IsBusy = true;
		try
		{
			await Services.TransactionBroadcaster.SendTransactionAsync(cancelTransaction);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			await ShowErrorAsync("Cancellation", ex.ToUserFriendlyString(), "Wasabi was unable to cancel your transaction.");
		}
		IsBusy = false;
		UiContext.Navigate().To().SendSuccess(_wallet, cancelTransaction);
	}

	public Money FeeDifference { get; }

	protected override void OnDialogClosed()
	{
	}
}
