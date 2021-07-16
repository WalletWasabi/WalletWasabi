using System;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcasting;
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
		[AutoNotify] private string _nextButtonText;
		[AutoNotify] private SmartLabel _labels;

		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, BuildTransactionResult transaction)
		{
			_wallet = wallet;
			_labels = SmartLabel.Empty;
			_info = info;
			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: false);
			EnableBack = true;
			_confirmationTimeText = "";

			var destinationAmount = transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			var btcAmountText = $"{destinationAmount} bitcoins ";
			var fiatAmountText = destinationAmount.GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");
			AmountText = $"{btcAmountText}{fiatAmountText}";

			AddressText = info.Address.ToString();

			var fee = transaction.Fee;
			var btcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} sats ";
			var fiatFeeText = fee.ToDecimal(MoneyUnit.BTC).GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");
			FeeText = $"{btcFeeText}{fiatFeeText}";

			PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
			IsPayJoin = PayJoinUrl is not null;

			if (PreferPsbtWorkflow)
			{
				SkipCommand = ReactiveCommand.CreateFromTask(async () => await OnConfirmAsync(transaction));
				NextCommand = ReactiveCommand.CreateFromTask(async () =>
				{
					var saved = await TransactionHelpers.ExportTransactionToBinaryAsync(transaction);

					if (saved)
					{
						Navigate().To(new SuccessViewModel("The PSBT has been successfully created."));
					}
				});
				_nextButtonText = "Save PSBT file";
			}
			else
			{
				NextCommand = ReactiveCommand.CreateFromTask(async () => await OnConfirmAsync(transaction));
				_nextButtonText = "Confirm";
			}

		}

		public bool PreferPsbtWorkflow => _wallet.KeyManager.PreferPsbtWorkflow;

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

		private async Task OnConfirmAsync(BuildTransactionResult transaction)
		{
			var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);

			var authResult = await AuthorizeAsync(transactionAuthorizationInfo);

			if (authResult)
			{
				IsBusy = true;

				try
				{
					var finalTransaction = await GetFinalTransactionAsync(transactionAuthorizationInfo.Transaction, _info);
					await SendTransactionAsync(finalTransaction);
					Navigate().To(new SendSuccessViewModel(_wallet, finalTransaction));
				}
				catch (Exception ex)
				{
					await ShowErrorAsync("Transaction", ex.ToUserFriendlyString(), "Wasabi was unable to send your transaction.");
				}

				IsBusy = false;
			}
		}

		private async Task<bool> AuthorizeAsync(TransactionAuthorizationInfo transactionAuthorizationInfo)
		{
			if (!_wallet.KeyManager.IsHardwareWallet && string.IsNullOrEmpty(_wallet.Kitchen.SaltSoup())) // Do not show auth dialog when password is empty
			{
				return true;
			}

			var authDialog = AuthorizationHelpers.GetAuthorizationDialog(_wallet, transactionAuthorizationInfo);
			var authDialogResult = await NavigateDialogAsync(authDialog, authDialog.DefaultTarget);

			if (!authDialogResult.Result && authDialogResult.Kind == DialogResultKind.Normal)
			{
				await ShowErrorAsync("Authorization", "The Authorization has failed, please try again.", "");
			}

			return authDialogResult.Result;
		}

		private async Task SendTransactionAsync( SmartTransaction transaction)
		{
			// Dequeue any coin-joining coins.
			await _wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.TransactionBuilding);

			await Services.TransactionBroadcaster.SendTransactionAsync(transaction);
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
