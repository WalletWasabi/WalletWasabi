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
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.CoinControl;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Transaction Preview")]
public partial class TransactionPreviewViewModel : RoutableViewModel
{
	private readonly Stack<(BuildTransactionResult, TransactionInfo)> _undoHistory;
	private readonly Wallet _wallet;
	private readonly WalletViewModel _walletViewModel;
	private TransactionInfo _info;
	private TransactionInfo _currentTransactionInfo;
	private CancellationTokenSource? _cancellationTokenSource;
	[AutoNotify] private BuildTransactionResult? _transaction;
	[AutoNotify] private string _nextButtonText;
	[AutoNotify] private bool _adjustFeeAvailable;
	[AutoNotify] private TransactionSummaryViewModel? _displayedTransactionSummary;
	[AutoNotify] private bool _canUndo;
	[AutoNotify] private bool _isCoinControlVisible;

	public TransactionPreviewViewModel(WalletViewModel walletViewModel, TransactionInfo info)
	{
		_undoHistory = new();
		_wallet = walletViewModel.Wallet;
		_walletViewModel = walletViewModel;
		_info = info;
		_currentTransactionInfo = info.Clone();
		_cancellationTokenSource = new CancellationTokenSource();

		PrivacySuggestions = new PrivacySuggestionsFlyoutViewModel();
		CurrentTransactionSummary = new TransactionSummaryViewModel(this, _wallet, _info);
		PreviewTransactionSummary = new TransactionSummaryViewModel(this, _wallet, _info, true);

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
			.Subscribe(x =>
			{
				PrivacySuggestions.IsOpen = false;
				PrivacySuggestions.SelectedSuggestion = null;

				if (x is ChangeAvoidanceSuggestionViewModel ca)
				{
					_info.ChangelessCoins = ca.TransactionResult.SpentCoins;
					UpdateTransaction(CurrentTransactionSummary, ca.TransactionResult);
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

		this.WhenAnyValue(x => x.Transaction)
			.WhereNotNull()
			.Throttle(TimeSpan.FromMilliseconds(100))
			.ObserveOn(RxApp.MainThreadScheduler)
			.DoAsync(async transaction => await PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, transaction, _cancellationTokenSource.Token))
			.Subscribe();

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

		AdjustFeeCommand = ReactiveCommand.CreateFromTask(OnAdjustFeeAsync);

		UndoCommand = ReactiveCommand.Create(() =>
		{
			if (_undoHistory.TryPop(out var previous))
			{
				_info = previous.Item2;
				UpdateTransaction(CurrentTransactionSummary, previous.Item1, false);
				CanUndo = _undoHistory.Any();
			}
		});

		ChangePocketCommand = ReactiveCommand.CreateFromTask(OnChangePocketsAsync);
		ChangeCoinsCommand = ReactiveCommand.CreateFromTask(OnChangeCoinsAsync);
	}

	public TransactionSummaryViewModel CurrentTransactionSummary { get; }

	public TransactionSummaryViewModel PreviewTransactionSummary { get; }

	public List<TransactionSummaryViewModel> TransactionSummaries { get; }

	public PrivacySuggestionsFlyoutViewModel PrivacySuggestions { get; }

	public bool PreferPsbtWorkflow => _wallet.KeyManager.PreferPsbtWorkflow;

	public ICommand AdjustFeeCommand { get; }

	public ICommand ChangePocketCommand { get; }

	public ICommand ChangeCoinsCommand { get; }

	public ICommand UndoCommand { get; }

	private async Task OnExportPsbtAsync()
	{
		if (Transaction is { })
		{
			var saved = await TransactionHelpers.ExportTransactionToBinaryAsync(Transaction);

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

			Transaction = transaction;
			CheckChangePocketAvailable(Transaction);
			_currentTransactionInfo = _info.Clone();
		}

		summary.UpdateTransaction(transaction, _info);

		DisplayedTransactionSummary = summary;
	}

	private async Task OnAdjustFeeAsync()
	{
		DialogViewModelBase<FeeRate> feeDialog = _info.IsCustomFeeUsed
			? new CustomFeeRateDialogViewModel(_info)
			: new SendFeeViewModel(_wallet, _info, false);

		var feeDialogResult = await NavigateDialogAsync(feeDialog, feeDialog.DefaultTarget);

		if (feeDialogResult.Kind == DialogResultKind.Normal &&
			feeDialogResult.Result is { } feeRate &&
			feeRate != _info.FeeRate) // Prevent rebuild if the selected fee did not change.
		{
			_info.FeeRate = feeRate;
			await BuildAndUpdateAsync(BuildTransactionReason.FeeChanged);
		}
	}

	private async Task BuildAndUpdateAsync(BuildTransactionReason reason)
	{
		var newTransaction = await BuildTransactionAsync(reason);

		if (newTransaction is { })
		{
			UpdateTransaction(CurrentTransactionSummary, newTransaction);
		}
	}

	private async Task OnChangePocketsAsync()
	{
		var selectPocketsDialog =
			await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, Transaction?.SpentCoins, false));

		if (selectPocketsDialog.Kind == DialogResultKind.Normal && selectPocketsDialog.Result is { })
		{
			_info.Coins = selectPocketsDialog.Result;
			_info.ChangelessCoins = Enumerable.Empty<SmartCoin>(); // Clear ChangelessCoins on pocket change, so we calculate the suggestions with the new pocket.
			await BuildAndUpdateAsync(BuildTransactionReason.PocketChanged);
		}
	}

	private async Task OnChangeCoinsAsync()
	{
		var selectedCoins = (Transaction?.SpentCoins ?? new List<SmartCoin>()).ToList();

		var selectCoinsDialog =
			await NavigateDialogAsync(new SelectCoinsDialogViewModel(_walletViewModel, selectedCoins));

		if (selectCoinsDialog.Kind == DialogResultKind.Normal && selectCoinsDialog.Result is { })
		{
			if (selectedCoins.SequenceEqual(selectCoinsDialog.Result))
			{
				return;
			}

			_info.Coins = selectCoinsDialog.Result;
			_info.ChangelessCoins = Enumerable.Empty<SmartCoin>(); // Clear ChangelessCoins on pocket change, so we calculate the suggestions with the new coins.
			await BuildAndUpdateAsync(BuildTransactionReason.PocketChanged);
		}
	}

	private async Task<bool> InitialiseTransactionAsync()
	{
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

		if (!_info.Coins.Any())
		{
			var privacyControlDialogResult =
				await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, Transaction?.SpentCoins, isSilent: true));
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

			return await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, _info, tryToSign: false));
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

		if (differenceOfFeePercentage is > 0 and < TransactionFeeHelper.FeePercentageThreshold ||
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
			if (selectedAmount == _info.Amount && !(_info.IsFixedAmount || _info.IsPayJoin))
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
				await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, usedCoins: Transaction?.SpentCoins, isSilent: doSilentPocketSelection));

			if (selectPocketsDialog.Kind == DialogResultKind.Normal && selectPocketsDialog.Result is { })
			{
				_info.Coins = selectPocketsDialog.Result;
				return true;
			}

			if (selectPocketsDialog.Kind != DialogResultKind.Normal)
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
			_cancellationTokenSource?.Cancel();
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = null;
			_info.ChangelessCoins = Enumerable.Empty<SmartCoin>(); // Clear ChangelessCoins on cancel, so the user can undo the optimization.
		}

		base.OnNavigatedFrom(isInHistory);

		DisplayedTransactionSummary = null;
	}

	private async Task OnConfirmAsync()
	{
		var transaction = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, _info));
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
				_cancellationTokenSource?.Cancel();
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
					TransactionHelpers.BuildTransaction(_wallet, transactionInfo, isPayJoin: true));
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
		if (Transaction is { })
		{
			_undoHistory.Push((Transaction, _currentTransactionInfo));
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
		var labelSelection = new LabelSelectionViewModel(_wallet.KeyManager, _wallet.Kitchen.SaltSoup(), _info, isSilent: true);
		labelSelection.Reset(pockets);

		_info.IsOtherPocketSelectionPossible = labelSelection.IsOtherSelectionPossible(usedCoins, _info.Recipient);
	}
}
