using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;

[NavigationMetaData(Title = "Cancel Transaction")]
public partial class CancelTransactionDialogViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;

	private CancelTransactionDialogViewModel(Wallet wallet, SmartTransaction transactionToCancel, BuildTransactionResult cancellingTransaction)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		var originalFee = transactionToCancel.WalletInputs.Sum(x => x.Amount) - transactionToCancel.OutputValues.Sum(x => x);
		var cancelFee = cancellingTransaction.Fee;
		FeeDifference = cancelFee - originalFee;
		TotalFee = cancelFee;
		FeeDifferenceUsd = FeeDifference.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate;
		TotalFeeUsd = TotalFee.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate;

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnCancelTransactionAsync(cancellingTransaction));
	}

	public decimal TotalFeeUsd { get; }

	public Money TotalFee { get; set; }

	public decimal FeeDifferenceUsd { get; }

	public Money FeeDifference { get; }

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
