using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Transaction Preview")]
public partial class TransactionPreviewViewModel : RoutableViewModel
{
	private readonly Stack<(BuildTransactionResult, TransactionInfo)> _undoHistory;
	private readonly bool _isFixedAmount;
	private readonly Wallet _wallet;
	private readonly BitcoinAddress _destination;
	private BuildTransactionResult? _transaction;
	private TransactionInfo _info;
	private TransactionInfo _currentTransactionInfo;
	private CancellationTokenSource _cancellationTokenSource;
	[AutoNotify] private string _nextButtonText;
	[AutoNotify] private bool _adjustFeeAvailable;
	[AutoNotify] private TransactionSummaryViewModel? _displayedTransactionSummary;
	[AutoNotify] private bool _canUndo;

	public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, BitcoinAddress destination, bool isFixedAmount)
	{
		_undoHistory = new();
		_wallet = wallet;
		_info = info;
		_currentTransactionInfo = info.Clone();
		_destination = destination;
		_isFixedAmount = isFixedAmount;
		_cancellationTokenSource = new CancellationTokenSource();

		PrivacySuggestions = new PrivacySuggestionsFlyoutViewModel();
		CurrentTransactionSummary = new TransactionSummaryViewModel(this, _wallet, _info, destination);
		PreviewTransactionSummary = new TransactionSummaryViewModel(this, _wallet, _info, destination, true);

		TransactionSummaries = new List<TransactionSummaryViewModel>
		{
			CurrentTransactionSummary,
			PreviewTransactionSummary
		};

		DisplayedTransactionSummary = CurrentTransactionSummary;

		PrivacySuggestions.WhenAnyValue(x => x.PreviewSuggestion)
			.Subscribe(x =>
			{
				if (x is ChangeAvoidanceSuggestionViewModel ca)
				{
					UpdateTransaction(PreviewTransactionSummary, ca.TransactionResult);
				}
				else
				{
					DisplayedTransactionSummary = CurrentTransactionSummary;
				}
			});

		PrivacySuggestions.WhenAnyValue(x => x.SelectedSuggestion)
			.SubscribeAsync(async x =>
			{
				PrivacySuggestions.IsOpen = false;
				PrivacySuggestions.SelectedSuggestion = null;

				if (x is ChangeAvoidanceSuggestionViewModel ca)
				{
					_info.ChangelessCoins = ca.TransactionResult.SpentCoins;
					UpdateTransaction(CurrentTransactionSummary, ca.TransactionResult);

					await PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, _destination, ca.TransactionResult, _isFixedAmount, _cancellationTokenSource.Token);
				}
			});

		PrivacySuggestions.WhenAnyValue(x => x.IsOpen)
			.Subscribe(x =>
			{
				if (!x)
				{
					DisplayedTransactionSummary = CurrentTransactionSummary;
				}
			});

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		AdjustFeeAvailable = !TransactionFeeHelper.AreTransactionFeesEqual(_wallet);

		if (PreferPsbtWorkflow)
		{
			SkipCommand = ReactiveCommand.CreateFromTask(OnConfirmAsync);

			NextCommand = ReactiveCommand.CreateFromTask(OnExportPsbtAsync);

			_nextButtonText = "Save PSBT file";
		}
		else
		{
			NextCommand = ReactiveCommand.CreateFromTask(OnConfirmAsync);

			_nextButtonText = "Confirm";
		}

		AdjustFeeCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (_info.IsCustomFeeUsed)
			{
				await ShowAdvancedDialogAsync();
			}
			else
			{
				await OnAdjustFeeAsync();
			}
		});

		UndoCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (_undoHistory.TryPop(out var previous))
			{
				_info = previous.Item2;
				UpdateTransaction(CurrentTransactionSummary, previous.Item1, false);
				CanUndo = _undoHistory.Any();
				await PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, _destination, previous.Item1, _isFixedAmount, _cancellationTokenSource.Token);
			}
		});

		ChangePocketCommand = ReactiveCommand.CreateFromTask(OnChangePocketsAsync);
	}

	public TransactionSummaryViewModel CurrentTransactionSummary { get; }

	public TransactionSummaryViewModel PreviewTransactionSummary { get; }

	public List<TransactionSummaryViewModel> TransactionSummaries { get; }

	public PrivacySuggestionsFlyoutViewModel PrivacySuggestions { get; }

	public bool PreferPsbtWorkflow => _wallet.KeyManager.PreferPsbtWorkflow;

	public ICommand AdjustFeeCommand { get; }

	public ICommand ChangePocketCommand { get; }

	public ICommand UndoCommand { get; }

	private async Task ShowAdvancedDialogAsync()
	{
		var result = await NavigateDialogAsync(new AdvancedSendOptionsViewModel(_info), NavigationTarget.CompactDialogScreen);

		if (result.Kind == DialogResultKind.Normal)
		{
			await BuildAndUpdateAsync(BuildTransactionReason.FeeChanged);
		}
	}

	private async Task OnExportPsbtAsync()
	{
		if (_transaction is { })
		{
			var saved = await TransactionHelpers.ExportTransactionToBinaryAsync(_transaction);

			if (saved)
			{
				Navigate().To(new SuccessViewModel("The PSBT has been successfully created."));
			}
		}
	}

	private void UpdateTransaction(TransactionSummaryViewModel summary, BuildTransactionResult transaction, bool addToUndoHistory = true)
	{
		if (!summary.IsPreview)
		{
			if (addToUndoHistory)
			{
				AddToUndoHistory();
			}

			_transaction = transaction;
			CheckChangePocketAvailable(_transaction);
			_currentTransactionInfo = _info.Clone();
		}

		summary.UpdateTransaction(transaction, _info);

		DisplayedTransactionSummary = summary;
	}

	private async Task OnAdjustFeeAsync()
	{
		var feeRateDialogResult = await NavigateDialogAsync(new SendFeeViewModel(_wallet, _info, false));

		if (feeRateDialogResult.Kind == DialogResultKind.Normal && feeRateDialogResult.Result is { } newFeeRate &&
		    newFeeRate != _info.FeeRate)
		{
			_info.FeeRate = feeRateDialogResult.Result;

			await BuildAndUpdateAsync(BuildTransactionReason.FeeChanged);
		}
	}

	private async Task BuildAndUpdateAsync(BuildTransactionReason reason)
	{
		var newTransaction = await BuildTransactionAsync(reason);

		if (newTransaction is { })
		{
			UpdateTransaction(CurrentTransactionSummary, newTransaction);

			await PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, _destination, newTransaction, _isFixedAmount, _cancellationTokenSource.Token);
		}
	}

	private async Task OnChangePocketsAsync()
	{
		var selectPocketsDialog =
			await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, _transaction?.SpentCoins, false));

		if (selectPocketsDialog.Kind == DialogResultKind.Normal && selectPocketsDialog.Result is { })
		{
			_info.Coins = selectPocketsDialog.Result;
			_info.ChangelessCoins = Enumerable.Empty<SmartCoin>(); // Clear ChangelessCoins on pocket change, so we calculate the suggestions with the new pocket.
			await BuildAndUpdateAsync(BuildTransactionReason.PocketChanged);
		}
	}

	private async Task<bool> InitialiseTransactionAsync()
	{
		if (!_info.Coins.Any())
		{
			var privacyControlDialogResult =
				await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, _transaction?.SpentCoins, isSilent: true));
			if (privacyControlDialogResult.Kind == DialogResultKind.Normal &&
			    privacyControlDialogResult.Result is { } coins)
			{
				_info.Coins = coins;
			}
			else if (privacyControlDialogResult.Kind != DialogResultKind.Normal)
			{
				return false;
			}
		}

		if (_info.FeeRate == FeeRate.Zero)
		{
			var feeDialogResult = await NavigateDialogAsync(new SendFeeViewModel(_wallet, _info, true));
			if (feeDialogResult.Kind == DialogResultKind.Normal && feeDialogResult.Result is { } newFeeRate)
			{
				_info.FeeRate = newFeeRate;
			}
			else
			{
				return false;
			}
		}

		return true;
	}

	private async Task<BuildTransactionResult?> BuildTransactionAsync(BuildTransactionReason reason)
	{
		if (!await InitialiseTransactionAsync())
		{
			return null;
		}

		try
		{
			IsBusy = true;

			return await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, _info, _destination, tryToSign: false));
		}
		catch (NotEnoughFundsException ex)
		{
			// TODO: Any other scenario when this exception happens?
			var totalFee = _info.Amount + (Money)ex.Missing;
			var percentage = ((decimal)totalFee.Satoshi / _info.Amount.Satoshi) * 100;

			var result = await TryAdjustTransactionFeeAsync(percentage);

			return result ? await BuildTransactionAsync(reason) : null;
		}
		catch (TransactionFeeOverpaymentException ex)
		{
			var result = await TryAdjustTransactionFeeAsync(ex.PercentageOfOverpayment);

			return result ? await BuildTransactionAsync(reason) : null;
		}
		catch (InsufficientBalanceException ex)
		{
			var failedTransactionFee = ex.Minimum - _info.Amount;
			var maxPossibleFeeWithSelectedCoins = ex.Actual - _info.Amount;
			var differenceOfFeePercentage = maxPossibleFeeWithSelectedCoins == Money.Zero ? 0M : (decimal)failedTransactionFee.Satoshi / maxPossibleFeeWithSelectedCoins.Satoshi * 100;

			var result = await TryHandleInsufficientBalanceCaseAsync(differenceOfFeePercentage, ex.Minimum, reason);

			return result ? await BuildTransactionAsync(reason) : null;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);

			await ShowErrorAsync("Transaction Building", ex.ToUserFriendlyString(),
				"Wasabi was unable to create your transaction.");

			return null;
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task<bool> TryAdjustTransactionFeeAsync(decimal percentageOfOverpayment)
	{
		var result = TransactionFeeHelper.TryGetMaximumPossibleFeeRate(percentageOfOverpayment, _wallet, _info.FeeRate, out var maximumPossibleFeeRate);

		if (!result)
		{
			await ShowErrorAsync("Transaction Building", "The transaction cannot be sent because its fee is more than the payment amount.",
				"Wasabi was unable to create your transaction.");

			return false;
		}

		_info.MaximumPossibleFeeRate = maximumPossibleFeeRate;
		_info.FeeRate = maximumPossibleFeeRate;
		_info.ConfirmationTimeSpan = TransactionFeeHelper.CalculateConfirmationTime(maximumPossibleFeeRate, _wallet);

		return true;
	}

	private async Task<bool> TryHandleInsufficientBalanceCaseAsync(decimal differenceOfFeePercentage, Money minimumRequiredAmount, BuildTransactionReason reason)
	{
		var maximumPossibleFeeRate =
			TransactionFeeHelper.TryGetMaximumPossibleFeeRate(differenceOfFeePercentage, _wallet, _info.FeeRate, out var feeRate)
				? feeRate
				: FeeRate.Zero;

		if (differenceOfFeePercentage is > 0 and TransactionFeeHelper.FeePercentageThreshold ||
		    (differenceOfFeePercentage > 0 && reason == BuildTransactionReason.FeeChanged))
		{
			_info.MaximumPossibleFeeRate = maximumPossibleFeeRate;
			_info.FeeRate = maximumPossibleFeeRate;
			_info.ConfirmationTimeSpan = TransactionFeeHelper.CalculateConfirmationTime(maximumPossibleFeeRate, _wallet);

			return true;
		}

		var selectedAmount = _info.Coins.Sum(x => x.Amount);
		var totalBalanceUsed = selectedAmount == _wallet.Coins.TotalAmount();

		if (totalBalanceUsed)
		{
			if (selectedAmount == _info.Amount && !(_isFixedAmount || _info.IsPayJoin))
			{
				_info.SubtractFee = true;
				return true;
			}
			else if (selectedAmount != _info.Amount && maximumPossibleFeeRate != FeeRate.Zero)
			{
				_info.MaximumPossibleFeeRate = maximumPossibleFeeRate;
				_info.FeeRate = maximumPossibleFeeRate;
				_info.ConfirmationTimeSpan = TransactionFeeHelper.CalculateConfirmationTime(maximumPossibleFeeRate, _wallet);
				return true;
			}
		}
		else
		{
			var doSilentPocketSelection = reason == BuildTransactionReason.Initialization;
			_info.MinimumRequiredAmount = minimumRequiredAmount;

			var selectPocketsDialog =
				await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, usedCoins: _transaction?.SpentCoins, isSilent: doSilentPocketSelection));

			if (selectPocketsDialog.Kind == DialogResultKind.Normal && selectPocketsDialog.Result is { })
			{
				_info.Coins = selectPocketsDialog.Result;
				return true;
			}
			else if (selectPocketsDialog.Kind != DialogResultKind.Normal)
			{
				return false;
			}
		}

		var errorMessage = maximumPossibleFeeRate == FeeRate.Zero
			? "There are not enough funds to cover the transaction fee."
			: "The transaction cannot be sent at the moment.";

		await ShowErrorAsync("Transaction Building", errorMessage,
			"Wasabi was unable to create your transaction.");
		return false;
	}

	private async Task InitialiseViewModelAsync()
	{
		if (await BuildTransactionAsync(BuildTransactionReason.Initialization) is { } initialTransaction)
		{
			UpdateTransaction(CurrentTransactionSummary, initialTransaction);

			await PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, _destination, initialTransaction, _isFixedAmount, _cancellationTokenSource.Token);
		}
		else
		{
			Navigate().Back();
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Observable
			.FromEventPattern(_wallet.FeeProvider, nameof(_wallet.FeeProvider.AllFeeEstimateChanged))
			.Subscribe(_ => AdjustFeeAvailable = !TransactionFeeHelper.AreTransactionFeesEqual(_wallet))
			.DisposeWith(disposables);

		if (!isInHistory)
		{
			RxApp.MainThreadScheduler.Schedule(async () => await InitialiseViewModelAsync());
		}
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		if (!isInHistory)
		{
			_cancellationTokenSource.Cancel();
			_cancellationTokenSource.Dispose();
			_info.ChangelessCoins = Enumerable.Empty<SmartCoin>(); // Clear ChangelessCoins on cancel, so the user can undo the optimization.
		}

		base.OnNavigatedFrom(isInHistory);

		DisplayedTransactionSummary = null;
	}

	private async Task OnConfirmAsync()
	{
		var transaction = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, _info, _destination));
		var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);
		var authResult = await AuthorizeAsync(transactionAuthorizationInfo);
		if (authResult)
		{
			IsBusy = true;

			try
			{
				var finalTransaction =
					await GetFinalTransactionAsync(transactionAuthorizationInfo.Transaction, _info);
				await SendTransactionAsync(finalTransaction);
				_cancellationTokenSource.Cancel();
				Navigate().To(new SendSuccessViewModel(_wallet, finalTransaction));
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync("Transaction", ex.ToUserFriendlyString(),
					"Wasabi was unable to send your transaction.");
			}

			IsBusy = false;
		}
	}

	private async Task<bool> AuthorizeAsync(TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		if (!_wallet.KeyManager.IsHardwareWallet &&
		    string.IsNullOrEmpty(_wallet.Kitchen.SaltSoup())) // Do not show authentication dialog when password is empty
		{
			return true;
		}

		var authDialog = AuthorizationHelpers.GetAuthorizationDialog(_wallet, transactionAuthorizationInfo);
		var authDialogResult = await NavigateDialogAsync(authDialog, authDialog.DefaultTarget, NavigationMode.Clear);

		return authDialogResult.Result;
	}

	private async Task SendTransactionAsync(SmartTransaction transaction)
	{
		await Services.TransactionBroadcaster.SendTransactionAsync(transaction);
	}

	private async Task<SmartTransaction> GetFinalTransactionAsync(SmartTransaction transaction,
		TransactionInfo transactionInfo)
	{
		if (transactionInfo.PayJoinClient is { })
		{
			try
			{
				var payJoinTransaction = await Task.Run(() =>
					TransactionHelpers.BuildTransaction(_wallet, transactionInfo, _destination, isPayJoin: true));
				return payJoinTransaction.Transaction;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		return transaction;
	}

	private void AddToUndoHistory()
	{
		if (_transaction is { })
		{
			_undoHistory.Push((_transaction, _currentTransactionInfo));
			CanUndo = true;
		}
	}

	private void CheckChangePocketAvailable(BuildTransactionResult transaction)
	{
		if (!_info.IsSelectedCoinModificationEnabled)
		{
			_info.IsOtherPocketSelectionPossible = false;
			return;
		}

		var usedCoins = transaction.SpentCoins;
		var pockets = _wallet.GetPockets().ToArray();
		var amount = _info.MinimumRequiredAmount == Money.Zero ? _info.Amount : _info.MinimumRequiredAmount;
		var labelSelection = new LabelSelectionViewModel(amount);
		labelSelection.Reset(pockets);

		_info.IsOtherPocketSelectionPossible = labelSelection.IsOtherSelectionPossible(usedCoins, _info.UserLabels);
	}
}
