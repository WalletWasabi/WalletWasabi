using System;
using System.Linq;
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
		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, TransactionBroadcaster broadcaster,
			BuildTransactionResult transaction)
		{
			EnableCancel = true;
			EnableBack = true;

			var destinationAmount = transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			var btcAmountText = $"{destinationAmount} bitcoins ";
			var fiatAmountText = $"(≈{(destinationAmount * wallet.Synchronizer.UsdExchangeRate).FormattedFiat()} USD) ";
			AmountText = $"{btcAmountText}{fiatAmountText}";

			Labels = info.Labels.Labels.ToArray();

			AddressText = info.Address.ToString();

			ConfirmationTimeText = $"Approximately {TextHelpers.TimeSpanToFriendlyString(info.ConfirmationTimeSpan)} ";

			var fee = transaction.Fee;
			var btcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} satoshis ";
			var fiatFeeText =
				$"(≈{(fee.ToDecimal(MoneyUnit.BTC) * wallet.Synchronizer.UsdExchangeRate).FormattedFiat()} USD)";
			FeeText = $"{btcFeeText}{fiatFeeText}";

			NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNext(wallet, broadcaster, transaction));
		}

		public string AmountText { get; }

		public string[] Labels { get; }

		public string AddressText { get; }

		public string ConfirmationTimeText { get; }

		public string FeeText { get; }

		private async Task OnNext(Wallet wallet, TransactionBroadcaster broadcaster, BuildTransactionResult transaction)
		{
			var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);
			var authDialog = AuthorizationHelpers.GetAuthorizationDialog(wallet, transactionAuthorizationInfo);
			var authDialogResult = await NavigateDialog(authDialog, authDialog.DefaultTarget);

			if (authDialogResult.Result)
			{
				await SendTransaction(wallet, broadcaster, transactionAuthorizationInfo.Transaction);
			}
			else if (authDialogResult.Kind == DialogResultKind.Normal)
			{
				await ShowErrorAsync("Authorization", "The Authorization has failed, please try again.", "");
			}
		}

		private async Task SendTransaction(Wallet wallet, TransactionBroadcaster broadcaster, SmartTransaction transaction)
		{
			IsBusy = true;

			// Dequeue any coin-joining coins.
			await wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.TransactionBuilding);

			await broadcaster.SendTransactionAsync(transaction);
			Navigate().Clear();

			IsBusy = false;
		}
	}
}
