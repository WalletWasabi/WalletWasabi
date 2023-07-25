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

[NavigationMetaData(Title = "Speed Up Transaction")]
public partial class SpeedUpTransactionDialogViewModel : RoutableViewModel
{
	private readonly Wallet _wallet;

	private SpeedUpTransactionDialogViewModel(Wallet wallet, SmartTransaction transactionToSpeedUp, BuildTransactionResult boostingTransaction)
	{
		_wallet = wallet;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnSpeedUpTransactionAsync(boostingTransaction));

		FeeDifference = GetFeeDifference(transactionToSpeedUp, boostingTransaction);
		FeeDifferenceUsd = FeeDifference.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate;
		AreWePayingTheFee = boostingTransaction.Transaction.GetWalletOutputs(_wallet.KeyManager).Any();
	}

	public decimal FeeDifferenceUsd { get; }

	public bool AreWePayingTheFee { get; }

	public Money FeeDifference { get; }

	public Money GetFeeDifference(SmartTransaction transactionToSpeedUp, BuildTransactionResult boostingTransaction)
	{
		var isCpfp = boostingTransaction.Transaction.Transaction.Inputs.Any(x => x.PrevOut.Hash == transactionToSpeedUp.GetHash());
		var boostingTransactionFee = boostingTransaction.Fee;

		if (isCpfp)
		{
			return boostingTransactionFee;
		}

		var originalFee = transactionToSpeedUp.WalletInputs.Sum(x => x.Amount) - transactionToSpeedUp.OutputValues.Sum(x => x);
		return boostingTransactionFee - originalFee;
	}

	private async Task OnSpeedUpTransactionAsync(BuildTransactionResult boostingTransaction)
	{
		IsBusy = true;

		try
		{
			var isAuthorized = await AuthorizeForPasswordAsync();
			if (isAuthorized)
			{
				await Services.TransactionBroadcaster.SendTransactionAsync(boostingTransaction.Transaction);
				_wallet.UpdateUsedHdPubKeysLabels(boostingTransaction.HdPubKeysWithNewLabels);
				UiContext.Navigate().To().SendSuccess(_wallet, boostingTransaction.Transaction);
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
