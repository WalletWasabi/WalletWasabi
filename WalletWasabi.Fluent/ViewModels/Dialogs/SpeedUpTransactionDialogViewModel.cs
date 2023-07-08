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

[NavigationMetaData(Title = "Speed Up Transaction")]
public partial class SpeedUpTransactionDialogViewModel : DialogViewModelBase<Unit>
{
	private readonly Wallet _wallet;

	private SpeedUpTransactionDialogViewModel(Wallet wallet, SmartTransaction newTransaction, SmartTransaction originalTransaction)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnSpeedUpTransactionAsync(newTransaction));

		FeeDifference = GetFeeDifference(newTransaction, originalTransaction);
		FeeDifferenceUsd = FeeDifference.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate;
		AreWePayingTheFee = newTransaction.GetWalletOutputs(_wallet.KeyManager).Any();
	}

	public decimal FeeDifferenceUsd { get; }

	public bool AreWePayingTheFee { get; }

	public Money FeeDifference { get; }

	public Money GetFeeDifference(SmartTransaction newTransaction, SmartTransaction originalTransaction)
	{
		var isCpfp = newTransaction.Transaction.Inputs.Any(x => x.PrevOut.Hash == originalTransaction.GetHash());
		var newTransactionFee = newTransaction.WalletInputs.Sum(x => x.Amount) - newTransaction.OutputValues.Sum(x => x);

		if (isCpfp)
		{
			return newTransactionFee;
		}

		var originalFee = originalTransaction.WalletInputs.Sum(x => x.Amount) - originalTransaction.OutputValues.Sum(x => x);
		return newTransactionFee - originalFee;
	}

	protected override void OnDialogClosed()
	{
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
			UiContext.Navigate().To().ShowErrorDialog(ex.ToUserFriendlyString(), "Speed Up Failed", "Wasabi was unable to speed up your transaction.");
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
