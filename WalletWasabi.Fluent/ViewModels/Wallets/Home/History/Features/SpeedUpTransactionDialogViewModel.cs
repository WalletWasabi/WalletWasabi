using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;

[NavigationMetaData(Title = "Speed Up Transaction", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class SpeedUpTransactionDialogViewModel : RoutableViewModel
{
	private readonly SmartTransaction _transactionToSpeedUp;
	private readonly UiTriggers _triggers;
	private readonly Wallet _wallet;

	private SpeedUpTransactionDialogViewModel(UiTriggers triggers, Wallet wallet, SmartTransaction transactionToSpeedUp, BuildTransactionResult boostingTransaction)
	{
		_triggers = triggers;
		_wallet = wallet;
		_transactionToSpeedUp = transactionToSpeedUp;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
		NextCommand = ReactiveCommand.CreateFromTask(() => OnSpeedUpTransactionAsync(boostingTransaction));

		FeeDifference = GetFeeDifference(transactionToSpeedUp, boostingTransaction);
		FeeDifferenceUsd = FeeDifference.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate;

		var originalForeignAmounts = transactionToSpeedUp.ForeignOutputs.Select(x => x.TxOut.Value).OrderBy(x => x).ToArray();
		var boostedForeignAmounts = boostingTransaction.Transaction.ForeignOutputs.Select(x => x.TxOut.Value).OrderBy(x => x).ToArray();

		// Note, if it's CPFP, then it is changed, but we shouldn't bother by it, due to the other condition.
		var areForeignAmountsUnchanged = originalForeignAmounts.SequenceEqual(boostedForeignAmounts);

		// If the foreign outputs are unchanged or we have an output, then we are paying the fee.
		AreWePayingTheFee = areForeignAmountsUnchanged || boostingTransaction.Transaction.GetWalletOutputs(_wallet.KeyManager).Any();
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

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_triggers.TransactionsUpdateTrigger
			.Select(_ => _wallet.GetTransaction(_transactionToSpeedUp.GetHash()))
			.WhereNotNull()
			.Where(s => s.Confirmed)
			.Do(_ => Navigate().Back())
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
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
				var (title, caption) = ("Success", "Your transaction has been successfully accelerated.");
				UiContext.Navigate().To().SendSuccess(_wallet, boostingTransaction.Transaction, title, caption, NavigationTarget.CompactDialogScreen);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			var msg = _transactionToSpeedUp.Confirmed ? "The transaction is already confirmed." : ex.ToUserFriendlyString();
			UiContext.Navigate().To().ShowErrorDialog(msg, "Speed Up Failed", "Wasabi was unable to speed up your transaction.", NavigationTarget.CompactDialogScreen);
		}

		IsBusy = false;
	}

	private async Task<bool> AuthorizeForPasswordAsync()
	{
		if (!string.IsNullOrEmpty(_wallet.Kitchen.SaltSoup()))
		{
			var result = UiContext.Navigate().To().PasswordAuthDialog(new WalletModel(_wallet));
			var dialogResult = await result.GetResultAsync();
			return dialogResult;
		}

		return true;
	}
}
