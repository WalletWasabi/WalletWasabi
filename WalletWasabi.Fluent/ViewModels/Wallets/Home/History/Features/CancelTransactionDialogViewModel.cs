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
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Features;

[NavigationMetaData(Title = "Cancel Transaction")]
public partial class CancelTransactionDialogViewModel : RoutableViewModel
{
	private readonly UiTriggers _triggers;
	private readonly Wallet _wallet;
	private readonly SmartTransaction _transactionToCancel;

	private CancelTransactionDialogViewModel(UiTriggers triggers, Wallet wallet, SmartTransaction transactionToCancel, BuildTransactionResult cancellingTransaction)
	{
		_triggers = triggers;
		_wallet = wallet;
		_transactionToCancel = transactionToCancel;
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

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_triggers.TransactionsUpdateTrigger
			.Select(_ => _wallet.GetTransactions().First(s => s.GetHash() == _transactionToCancel.GetHash()))
			.Select(x => x.Confirmed)
			.Where(isConfirmed => isConfirmed)
			.Do(_ => Navigate().Back())
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
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
				_wallet.UpdateUsedHdPubKeysLabels(cancellingTransaction.HdPubKeysWithNewLabels);
				UiContext.Navigate().To().SendSuccess(_wallet, cancellingTransaction.Transaction);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);

			var msg = _transactionToCancel.Confirmed ? "The transaction is already confirmed." : ex.ToUserFriendlyString();

			UiContext.Navigate().To().ShowErrorDialog(msg, "Cancellation Failed", "Wasabi was unable to cancel your transaction.");
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
