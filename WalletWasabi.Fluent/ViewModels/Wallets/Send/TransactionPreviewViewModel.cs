using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Transaction Preview")]
	public partial class TransactionPreviewViewModel : RoutableViewModel
	{
		private readonly TransactionInfo _info;

		[AutoNotify] private string[]? _labels;

		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, TransactionBroadcaster broadcaster,
			BuildTransactionResult transaction)
		{
			_info = info;
			EnableCancel = true;
			EnableBack = true;

			var destinationAmount = transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			var btcAmountText = $"{destinationAmount} bitcoins ";
			var fiatAmountText = destinationAmount.GenerateFiatText(wallet.Synchronizer.UsdExchangeRate, "USD");
			AmountText = $"{btcAmountText}{fiatAmountText}";

			AddressText = info.Address.ToString();

			ConfirmationTimeText = $"Approximately {TextHelpers.TimeSpanToFriendlyString(info.ConfirmationTimeSpan)} ";

			var fee = transaction.Fee;
			var btcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} satoshis ";
			var fiatFeeText = fee.ToDecimal(MoneyUnit.BTC).GenerateFiatText(wallet.Synchronizer.UsdExchangeRate, "USD");
			FeeText = $"{btcFeeText}{fiatFeeText}";

			NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNext(wallet, broadcaster, transaction));
		}

		public string AmountText { get; }

		public string AddressText { get; }

		public string ConfirmationTimeText { get; }

		public string FeeText { get; }

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			Labels = _info.PocketLabels is { } ? _info.Labels.Concat(_info.PocketLabels).ToArray() : _info.Labels.ToArray();
		}

		private async Task OnNext(Wallet wallet, TransactionBroadcaster broadcaster, BuildTransactionResult transaction)
		{
			var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);

			var authResult = await AuthorizeAsync(wallet, transactionAuthorizationInfo);

			if (authResult)
			{
				await SendTransaction(wallet, broadcaster, transactionAuthorizationInfo.Transaction);
				Navigate().To(new SendSuccessViewModel());
			}
		}

		private async Task<bool> AuthorizeAsync(Wallet wallet, TransactionAuthorizationInfo transactionAuthorizationInfo)
		{
			if (!wallet.KeyManager.IsHardwareWallet && string.IsNullOrEmpty(wallet.Kitchen.SaltSoup())) // Do not show auth dialog when password is empty
			{
				return true;
			}

			var authDialog = AuthorizationHelpers.GetAuthorizationDialog(wallet, transactionAuthorizationInfo);
			var authDialogResult = await NavigateDialog(authDialog, authDialog.DefaultTarget);

			if (!authDialogResult.Result && authDialogResult.Kind == DialogResultKind.Normal)
			{
				await ShowErrorAsync("Authorization", "The Authorization has failed, please try again.", "");
			}

			return authDialogResult.Result;
		}

		private async Task SendTransaction(Wallet wallet, TransactionBroadcaster broadcaster, SmartTransaction transaction)
		{
			IsBusy = true;

			// Dequeue any coin-joining coins.
			await wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.TransactionBuilding);

			await broadcaster.SendTransactionAsync(transaction);

			IsBusy = false;
		}
	}
}
