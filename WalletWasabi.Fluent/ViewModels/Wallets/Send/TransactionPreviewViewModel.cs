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
		private readonly Wallet _wallet;
		private readonly TransactionInfo _info;

		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, TransactionBroadcaster broadcaster,
			BuildTransactionResult transaction)
		{
			_wallet = wallet;
			_info = info;
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

			PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
			IsPayJoin = PayJoinUrl is { };

			NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNext(wallet, broadcaster, transaction));
		}

		public string AmountText { get; }

		public string[] Labels { get; }

		public string AddressText { get; }

		public string ConfirmationTimeText { get; }

		public string FeeText { get; }

		public string? PayJoinUrl { get; }

		public bool IsPayJoin { get; }

		private async Task OnNext(Wallet wallet, TransactionBroadcaster broadcaster, BuildTransactionResult transaction)
		{
			var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);

			var authResult = await AuthorizeAsync(wallet, transactionAuthorizationInfo);

			if (authResult)
			{
				try
				{
					var finalTransaction = GetFinalTransaction(transactionAuthorizationInfo.Transaction, _info);
					await SendTransaction(wallet, broadcaster, finalTransaction);
					Navigate().To(new SendSuccessViewModel());
				}
				catch (Exception ex)
				{
					await ShowErrorAsync("Transaction", ex.ToUserFriendlyString(), "Wasabi was unable to send your transaction.");
				}
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

		private SmartTransaction GetFinalTransaction(SmartTransaction transaction, TransactionInfo transactionInfo)
		{
			if (transactionInfo.PayJoinClient is null)
			{
				return transaction;
			}

			var payJoinTransaction = TransactionHelpers.BuildTransaction(_wallet, transactionInfo, subtractFee: false, isPayJoin: true);

			return payJoinTransaction.Transaction;
		}
	}
}
