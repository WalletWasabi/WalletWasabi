using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Transaction Preview")]
	public partial class TransactionPreviewViewModel : RoutableViewModel
	{
		private readonly Wallet _wallet;
		private readonly TransactionInfo _info;

		[AutoNotify] private string _confirmationTimeText;
		[AutoNotify] private SmartLabel _labels;

		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, TransactionBroadcaster broadcaster,
			BuildTransactionResult transaction)
		{
			_wallet = wallet;
			_info = info;
			EnableCancel = true;
			EnableBack = true;
			_confirmationTimeText = "";

			var destinationAmount = transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			var btcAmountText = $"{destinationAmount} bitcoins ";
			var fiatAmountText = destinationAmount.GenerateFiatText(wallet.Synchronizer.UsdExchangeRate, "USD");
			AmountText = $"{btcAmountText}{fiatAmountText}";

			AddressText = info.Address.ToString();

			var fee = transaction.Fee;
			var btcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} satoshis ";
			var fiatFeeText = fee.ToDecimal(MoneyUnit.BTC).GenerateFiatText(wallet.Synchronizer.UsdExchangeRate, "USD");
			FeeText = $"{btcFeeText}{fiatFeeText}";

			PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
			IsPayJoin = PayJoinUrl is { };

			NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNext(wallet, broadcaster, transaction));
		}

		public string AmountText { get; }

		public string AddressText { get; }

		public string FeeText { get; }

		public string? PayJoinUrl { get; }

		public bool IsPayJoin { get; }

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			ConfirmationTimeText = $"Approximately {TextHelpers.TimeSpanToFriendlyString(_info.ConfirmationTimeSpan)} ";
			Labels = _info.Labels;
		}

		private async Task OnNext(Wallet wallet, TransactionBroadcaster broadcaster, BuildTransactionResult transaction)
		{
			var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);

			var authResult = await AuthorizeAsync(wallet, transactionAuthorizationInfo);

			if (authResult)
			{
				IsBusy = true;

				try
				{
					var finalTransaction = await GetFinalTransactionAsync(transactionAuthorizationInfo.Transaction, _info);
					await SendTransaction(wallet, broadcaster, finalTransaction);
					Navigate().To(new SendSuccessViewModel());
				}
				catch (Exception ex)
				{
					await ShowErrorAsync("Transaction", ex.ToUserFriendlyString(), "Wasabi was unable to send your transaction.");
				}

				IsBusy = false;
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
			// Dequeue any coin-joining coins.
			await wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.TransactionBuilding);

			await broadcaster.SendTransactionAsync(transaction);
		}

		private async Task<SmartTransaction> GetFinalTransactionAsync(SmartTransaction transaction, TransactionInfo transactionInfo)
		{
			if (transactionInfo.PayJoinClient is { })
			{
				try
				{
					var payJoinTransaction = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo, subtractFee: false, isPayJoin: true));
					return payJoinTransaction.Transaction;
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}
			}

			return transaction;
		}
	}
}
