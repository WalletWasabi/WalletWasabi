using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs;
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
		private BuildTransactionResult? _transaction;

		[AutoNotify] private string _confirmationTimeText;
		[AutoNotify] private string _feeText;
		[AutoNotify] private string _nextButtonText;
		[AutoNotify] private SmartLabel _labels;
		[AutoNotify] private string _amountText;

		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info)
		{
			_wallet = wallet;
			_labels = SmartLabel.Empty;
			_info = info;

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: false);

			EnableBack = true;
			_confirmationTimeText = "";

			AddressText = info.Address.ToString();

			PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
			IsPayJoin = PayJoinUrl is not null;

			if (PreferPsbtWorkflow)
			{
				SkipCommand = ReactiveCommand.CreateFromTask(async () => await OnConfirmAsync(_transaction));

				NextCommand = ReactiveCommand.CreateFromTask(async () =>
				{
					var saved = await TransactionHelpers.ExportTransactionToBinaryAsync(_transaction);

					if (saved)
					{
						Navigate().To(new SuccessViewModel("The PSBT has been successfully created."));
					}
				});

				_nextButtonText = "Save PSBT file";
			}
			else
			{
				NextCommand = ReactiveCommand.CreateFromTask(async () => await OnConfirmAsync(_transaction));

				_nextButtonText = "Confirm";
			}

			AdjustFeeCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var feeRateDialogResult = await NavigateDialogAsync(new SendFeeViewModel(wallet, info, false));

				if (feeRateDialogResult.Kind == DialogResultKind.Normal)
				{
					_info.FeeRate = feeRateDialogResult.Result;
				}

				await BuildTransactionAsync();

				UpdatePreview();
			});
		}

		public bool PreferPsbtWorkflow => _wallet.KeyManager.PreferPsbtWorkflow;

		public string AddressText { get; }

		public string? PayJoinUrl { get; }

		public bool IsPayJoin { get; }

		public ICommand AdjustFeeCommand { get; }

		private async Task<BuildTransactionResult?> BuildTransactionAsync()
		{
			var transactionInfo = _info;
			var targetAnonymitySet = _wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();
			var mixedCoins = _wallet.Coins.Where(x => x.HdPubKey.AnonymitySet >= targetAnonymitySet).ToList();
			var totalMixedCoinsAmount = Money.FromUnit(mixedCoins.Sum(coin => coin.Amount), MoneyUnit.Satoshi);
			transactionInfo.Coins = mixedCoins;

			if (transactionInfo.FeeRate is null)
			{
				var feeDialogResult = await NavigateDialogAsync(new SendFeeViewModel(_wallet, transactionInfo, true));

				if (feeDialogResult.Kind == DialogResultKind.Normal)
				{
					transactionInfo.FeeRate = feeDialogResult.Result;
				}
			}

			if (transactionInfo.Amount > totalMixedCoinsAmount && transactionInfo.Coins is null)
			{
				var privacyControlDialogResult = await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, transactionInfo));

				if (privacyControlDialogResult.Kind == DialogResultKind.Normal && privacyControlDialogResult.Result is { })
				{
					transactionInfo.Coins = privacyControlDialogResult.Result;
				}
			}

			try
			{
				if (transactionInfo.PayJoinClient is { })
				{
					return await BuildTransactionAsPayJoinAsync(transactionInfo);
				}
				else
				{
					return await BuildTransactionAsNormalAsync(transactionInfo, totalMixedCoinsAmount);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);

				await ShowErrorAsync("Transaction Building", ex.ToUserFriendlyString(),
					"Wasabi was unable to create your transaction.");

				return null;
			}
		}

		private void UpdatePreview()
		{
			if (_transaction is { })
			{
				var destinationAmount = _transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
				var btcAmountText = $"{destinationAmount} bitcoins ";
				var fiatAmountText =
					destinationAmount.GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");
				AmountText = $"{btcAmountText}{fiatAmountText}";

				var fee = _transaction.Fee;
				var btcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} sats ";
				var fiatFeeText = fee.ToDecimal(MoneyUnit.BTC)
					.GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");
				FeeText = $"{btcFeeText}{fiatFeeText}";
			}
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			ConfirmationTimeText = $"Approximately {TextHelpers.TimeSpanToFriendlyString(_info.ConfirmationTimeSpan)} ";
			Labels = _info.Labels;

			if (!isInHistory)
			{
				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					var transaction = await BuildTransactionAsync();

					_transaction = transaction;
				});
			}
		}

		private async Task<BuildTransactionResult> BuildTransactionAsNormalAsync(TransactionInfo transactionInfo, Money totalMixedCoinsAmount)
		{
			try
			{
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));

				return txRes;
			}
			catch (InsufficientBalanceException)
			{
				return null;

				// @soosr - need help here ;)
				/*
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo.Address,
					totalMixedCoinsAmount, transactionInfo.Labels, transactionInfo.FeeRate!, transactionInfo.Coins,
					subtractFee: true));

				var dialog = new InsufficientBalanceDialogViewModel(BalanceType.Private, txRes,
					_wallet.Synchronizer.UsdExchangeRate);

				var result = await NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);

				if (result.Result)
				{

					await NavigateDialogAsync(new OptimisePrivacyViewModel(_wallet, transactionInfo, txRes));
				}
				else
				{
					await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, transactionInfo));
				}*/
			}
		}

		private async Task<BuildTransactionResult> BuildTransactionAsPayJoinAsync(TransactionInfo transactionInfo)
		{
			try
			{
				// Do not add the PayJoin client yet, it will be added before broadcasting.
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));

				return txRes;
			}
			catch (InsufficientBalanceException)
			{
				await ShowErrorAsync("Transaction Building",
					"There are not enough private funds to cover the transaction fee",
					"Wasabi was unable to create your transaction.");
				//Navigate().To(new PrivacyControlViewModel(_wallet, transactionInfo), _isSilent ? NavigationMode.Skip : NavigationMode.Normal);

				return null;
			}
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
