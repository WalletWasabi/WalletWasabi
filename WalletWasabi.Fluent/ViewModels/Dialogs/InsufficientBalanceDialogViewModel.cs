using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Insufficient Balance")]
public partial class InsufficientBalanceDialogViewModel : DialogViewModelBase<Unit>
{
	private readonly InsufficientBalanceException _ex;
	private readonly Wallet _wallet;
	private readonly TransactionInfo _transactionInfo;
	private readonly decimal _differenceOfFeePercentage;

	[AutoNotify] private string? _question;

	private FeeRate _maximumPossibleFee = FeeRate.Zero;
	private TimeSpan _confirmationTimeWithMaxFee = TimeSpan.Zero;

	public InsufficientBalanceDialogViewModel(InsufficientBalanceException ex, Wallet wallet, TransactionInfo transactionInfo)
	{
		_ex = ex;
		_wallet = wallet;
		_transactionInfo = transactionInfo;

		var selectedFee = ex.Minimum - _transactionInfo.Amount;
		var maxPossibleFeeWithCurrentCoins = ex.Actual - _transactionInfo.Amount;
		_differenceOfFeePercentage = maxPossibleFeeWithCurrentCoins == Money.Zero ? 0M : (decimal)selectedFee.Satoshi / maxPossibleFeeWithCurrentCoins.Satoshi * 100;

		EnableSendAnyway = maxPossibleFeeWithCurrentCoins != Money.Zero;
		EnableSelectMoreCoin = _wallet.Coins.TotalAmount() >= ex.Minimum;
		EnableSubtractFee = _wallet.Coins.TotalAmount() == _transactionInfo.Amount;

		SendAnywayCommand = ReactiveCommand.Create(OnSendAnyway);
		SelectMoreCoinCommand = ReactiveCommand.CreateFromTask(OnSelectMoreCoinAsync);
		SubtractTransactionFeeCommand = ReactiveCommand.Create(OnSubtractTransactionFee);

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;
	}

	public ICommand SendAnywayCommand { get; }

	public ICommand SubtractTransactionFeeCommand { get; }

	public ICommand SelectMoreCoinCommand { get; }

	public bool EnableSendAnyway { get; }

	public bool EnableSelectMoreCoin { get; }

	public bool EnableSubtractFee { get; }

	private async Task OnSelectMoreCoinAsync()
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
			Close();
		}
		else
		{
			Close(DialogResultKind.Back);
		}
	}

	private void OnSubtractTransactionFee()
	{
		_transactionInfo.SubtractFee = true;
		Close();
	}

	private void OnSendAnyway()
	{
		_transactionInfo.MaximumPossibleFeeRate = _maximumPossibleFee;
		_transactionInfo.FeeRate = _maximumPossibleFee;
		_transactionInfo.ConfirmationTimeSpan = _confirmationTimeWithMaxFee;
		Close();
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (!isInHistory)
		{
			RxApp.MainThreadScheduler.Schedule(async () =>
			{
				// Edge cases, probably never will happen.
				if (!EnableSendAnyway && !EnableSubtractFee && !EnableSelectMoreCoin)
				{
					await ShowErrorAsync("Transaction Building",
						"Automatic transaction fee selection is not possible at the moment. Alternatively, you can enter the transaction fee manually in Advanced options.",
						"Wasabi was unable to create your transaction.");
					Close(DialogResultKind.Back);
					return;
				}

				if (_transactionInfo.IsPayJoin && _wallet.Coins.TotalAmount() == _transactionInfo.Amount)
				{
					await ShowErrorAsync("Transaction Building", "There are not enough funds to cover the transaction fee.", "Wasabi was unable to create your transaction.");
					Close(DialogResultKind.Back);
					return;
				}

				IsBusy = true;
				await CheckSilentCaseAsync(_differenceOfFeePercentage, _wallet, _transactionInfo);
				SetQuestion();
				IsBusy = false;
			});
		}
	}

	private async Task CheckSilentCaseAsync(decimal percentage, Wallet wallet, TransactionInfo transactionInfo)
	{
		if (percentage == 0)
		{
			return;
		}

		if (!TransactionFeeHelper.TryGetMaximumPossibleFee(percentage, wallet, transactionInfo.FeeRate, out var maxFee))
		{
			await ShowErrorAsync("Transaction Building",
				"Automatic transaction fee selection is not possible at the moment. Alternatively, you can enter the transaction fee manually in Advanced options.",
				"Wasabi was unable to create your transaction.");
			Close(DialogResultKind.Back);
			return;
		}

		_maximumPossibleFee = maxFee;
		_confirmationTimeWithMaxFee = TransactionFeeHelper.CalculateConfirmationTime(maxFee, wallet);

		if (percentage <= TransactionFeeHelper.FeePercentageThreshold)
		{
			OnSendAnyway();
		}
	}

	private void SetQuestion()
	{
		var sendingTotalBalance = _wallet.Coins.TotalAmount() == _transactionInfo.Amount;

		Question = $"There are not enough funds {(!sendingTotalBalance ? "selected " : "")}to cover the preferred transaction fee. Alternatively, ";

		if (EnableSubtractFee)
		{
			Question += "you can subtract the transaction fee from the amount.";
			return;
		}

		if (EnableSendAnyway)
		{
			Question += $"you can send the transaction with an adjusted transaction fee, so it should be confirmed within approximately {TextHelpers.TimeSpanToFriendlyString(_confirmationTimeWithMaxFee)}";

			if (EnableSelectMoreCoin)
			{
				Question += " or select more coins.";
			}
			else
			{
				Question += ".";
			}
		}
		else
		{
			if (EnableSelectMoreCoin)
			{
				Question += "you can select more coins.";
			}
		}
	}
}
