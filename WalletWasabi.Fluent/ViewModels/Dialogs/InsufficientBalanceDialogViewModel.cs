using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "Insufficient Balance")]
	public partial class InsufficientBalanceDialogViewModel : DialogViewModelBase<InsufficientBalanceUserDecision>
	{
		private readonly InsufficientBalanceException _ex;
		private readonly Wallet _wallet;
		private readonly TransactionInfo _transactionInfo;
		private readonly decimal _differenceOfFeePercentage;

		private FeeRate _maximumPossibleFee = FeeRate.Zero;
		private TimeSpan _confirmationTimeWithMaxFee = TimeSpan.Zero;

		public InsufficientBalanceDialogViewModel(InsufficientBalanceException ex, Wallet wallet, TransactionInfo transactionInfo)
		{
			_ex = ex;
			_wallet = wallet;
			_transactionInfo = transactionInfo;

			var selectedFee = ex.Minimum - _transactionInfo.Amount;
			var maxPossibleFee = ex.Actual - _transactionInfo.Amount;
			_differenceOfFeePercentage = maxPossibleFee == Money.Zero ? 0M : (decimal)selectedFee.Satoshi / maxPossibleFee.Satoshi * 100;

			EnableSendAnyway = maxPossibleFee != Money.Zero;
			EnableSelectMoreCoin = _wallet.Coins.TotalAmount() >= ex.Minimum;
			EnableSubtractFee = _wallet.Coins.TotalAmount() == _transactionInfo.Amount;

			Question = "What to do";

			NextCommand = ReactiveCommand.CreateFromTask<InsufficientBalanceUserDecision>(CompleteAsync);

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
			EnableBack = true;
		}

		public bool EnableSendAnyway { get; }

		public bool EnableSelectMoreCoin { get; }

		public bool EnableSubtractFee { get; }

		public string Question { get; }

		private async Task CompleteAsync(InsufficientBalanceUserDecision decision)
		{
			switch (decision)
			{
				case InsufficientBalanceUserDecision.SendAnyway:
					_transactionInfo.MaximumPossibleFeeRate = _maximumPossibleFee;
					_transactionInfo.FeeRate = _maximumPossibleFee;
					_transactionInfo.ConfirmationTimeSpan = _confirmationTimeWithMaxFee;
					break;

				case InsufficientBalanceUserDecision.SubtractTransactionFee:
					_transactionInfo.SubtractFee = true;
					break;

				case InsufficientBalanceUserDecision.SelectMoreCoin:
				{
					var privacyControlDialogResult = await NavigateDialogAsync(new PrivacyControlViewModel(
							_wallet,
							_transactionInfo,
							isSilent: false,
							targetAmount: _ex.Minimum),
						NavigationTarget.DialogScreen);

					if (privacyControlDialogResult.Result is { })
					{
						_transactionInfo.Coins = privacyControlDialogResult.Result;
					}
					else
					{
						Close();
						return;
					}

					break;
				}
			}

			Close(DialogResultKind.Normal, decision);
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			RxApp.MainThreadScheduler.Schedule(async () =>
			{
				IsBusy = true;
				await CheckSilentCaseAsync(_differenceOfFeePercentage, _wallet, _transactionInfo);
				IsBusy = false;
			});
		}

		private async Task CheckSilentCaseAsync(decimal percentage, Wallet wallet, TransactionInfo transactionInfo)
		{
			if (percentage == 0)
			{
				return;
			}

			if (!TransactionFeeHelper.TryGetMaximumPossibleFee(percentage, wallet, transactionInfo.FeeRate, out var maxFee))
			{
				// TODO: add message.
				// Subtract fee?
				await ShowErrorAsync("Transaction Building", "", "Wasabi was unable to create your transaction.");
				Close();
				return;
			}

			_maximumPossibleFee = maxFee;
			_confirmationTimeWithMaxFee = TransactionFeeHelper.CalculateConfirmationTime(maxFee, wallet);

			if (percentage <= TransactionFeeHelper.FeePercentageThreshold)
			{
				await CompleteAsync(InsufficientBalanceUserDecision.SendAnyway);
			}
		}
	}
}
