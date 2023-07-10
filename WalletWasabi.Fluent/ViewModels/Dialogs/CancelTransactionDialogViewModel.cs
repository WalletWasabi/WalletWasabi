using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Cancel Transaction")]
public partial class CancelTransactionDialogViewModel : DialogViewModelBase<Unit>
{
	private readonly Wallet _wallet;

	private CancelTransactionDialogViewModel(Wallet wallet, SmartTransaction transactionToCancel, BuildTransactionResult cancellingTransaction)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		var originalFee = transactionToCancel.WalletInputs.Sum(x => x.Amount) - transactionToCancel.OutputValues.Sum(x => x);
		var cancelFeel = cancellingTransaction.Fee;
		FeeDifference = cancelFeel - originalFee;
		FeeDifferenceUsd = FeeDifference.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate;

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnCancelTransactionAsync(cancellingTransaction));
	}

	public decimal FeeDifferenceUsd { get; set; }

	public Money FeeDifference { get; }

	protected override void OnDialogClosed()
	{
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
				UiContext.Navigate().To().SendSuccess(_wallet, cancellingTransaction.Transaction);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), "Speed Up failed", "Wasabi was unable to cancel your transaction.");
		}

		IsBusy = false;
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
