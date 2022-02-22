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
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Transaction Preview")]
public partial class TransactionPreviewViewModel : RoutableViewModel
{
	private readonly bool _isFixedAmount;
	private readonly Wallet _wallet;
	private readonly TransactionInfo _info;
	private readonly BitcoinAddress _destination;
	private BuildTransactionResult? _transaction;
	private CancellationTokenSource _cancellationTokenSource;
	[AutoNotify] private string _nextButtonText;
	[AutoNotify] private bool _adjustFeeAvailable;
	[AutoNotify] private TransactionSummaryViewModel? _displayedTransactionSummary;

	public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, BitcoinAddress destination, bool isFixedAmount)
	{
		_wallet = wallet;
		_info = info;
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
			.Subscribe(async x =>
			{
				PrivacySuggestions.IsOpen = false;
				PrivacySuggestions.SelectedSuggestion = null;

				if (x is ChangeAvoidanceSuggestionViewModel ca)
				{
					_info.ChangelessCoins = ca.TransactionResult.SpentCoins;
					UpdateTransaction(CurrentTransactionSummary, ca.TransactionResult);

					await PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, _destination, ca.TransactionResult, _isFixedAmount, _cancellationTokenSource.Token);
				}
				else if (x is PocketSuggestionViewModel)
				{
					await OnChangePocketsAsync();
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
	}

	public TransactionSummaryViewModel CurrentTransactionSummary { get; }

	public TransactionSummaryViewModel PreviewTransactionSummary { get; }

	public List<TransactionSummaryViewModel> TransactionSummaries { get; }

	public PrivacySuggestionsFlyoutViewModel PrivacySuggestions { get; }

	public bool PreferPsbtWorkflow => _wallet.KeyManager.PreferPsbtWorkflow;

	public ICommand AdjustFeeCommand { get; }

	private async Task ShowAdvancedDialogAsync()
	{
		var result = await NavigateDialogAsync(new AdvancedSendOptionsViewModel(_info), NavigationTarget.CompactDialogScreen);

		if (result.Kind == DialogResultKind.Normal)
		{
			await BuildAndUpdateAsync();
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

	private void UpdateTransaction(TransactionSummaryViewModel summary, BuildTransactionResult transaction)
	{
		if (!summary.IsPreview)
		{
			_transaction = transaction;
		}

		summary.UpdateTransaction(transaction);

		DisplayedTransactionSummary = summary;
	}

	private async Task OnAdjustFeeAsync()
	{
		var feeRateDialogResult = await NavigateDialogAsync(new SendFeeViewModel(_wallet, _info, false));

		if (feeRateDialogResult.Kind == DialogResultKind.Normal && feeRateDialogResult.Result is { } newFeeRate &&
			newFeeRate != _info.FeeRate)
		{
			_info.FeeRate = feeRateDialogResult.Result;

			await BuildAndUpdateAsync();
		}
	}

	private async Task BuildAndUpdateAsync()
	{
		var newTransaction = await BuildTransactionAsync();

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
			_info.ChangelessCoins = Enumerable.Empty<SmartCoin>();  // Clear ChangelessCoins on pocket change, so we calculate the suggestions with the new pocket.
			await BuildAndUpdateAsync();
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

	private async Task<BuildTransactionResult?> BuildTransactionAsync()
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
		catch (TransactionFeeOverpaymentException ex)
		{
			var result = TrySetMaximumPossibleFee(ex.PercentageOfOverpayment, _wallet, _info);

			if (!result)
			{
				await ShowErrorAsync("Transaction Building", "The transaction cannot be sent because its fee is more than the payment amount.",
					"Wasabi was unable to create your transaction.");

				return null;
			}

			return await BuildTransactionAsync();
		}
		catch (InsufficientBalanceException)
		{
			if (_info.IsPayJoin || _isFixedAmount)
			{
				return await HandleInsufficientBalanceWhenFixedAmountAsync(_wallet, _info);
			}

			return await HandleInsufficientBalanceWhenNormalAsync(_wallet, _info);
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

	private bool TrySetMaximumPossibleFee(decimal percentageOfOverpayment, Wallet wallet, TransactionInfo transactionInfo)
	{
		var currentFeeRate = transactionInfo.FeeRate;
		var maxPossibleFeeRateInSatoshiPerByte = (currentFeeRate.SatoshiPerByte / percentageOfOverpayment) * 100;
		var maximumPossibleFeeRate = new FeeRate(maxPossibleFeeRateInSatoshiPerByte);

		var feeChartViewModel = new FeeChartViewModel();
		feeChartViewModel.UpdateFeeEstimates(TransactionFeeHelper.GetFeeEstimates(wallet));

		var blockTarget = feeChartViewModel.GetConfirmationTarget(maximumPossibleFeeRate);
		var newFeeRate = new FeeRate(feeChartViewModel.GetSatoshiPerByte(blockTarget));

		if (newFeeRate > maximumPossibleFeeRate)
		{
			return false;
		}

		transactionInfo.ConfirmationTimeSpan = TransactionFeeHelper.CalculateConfirmationTime(blockTarget);
		transactionInfo.FeeRate = newFeeRate;
		transactionInfo.MaximumPossibleFeeRate = maximumPossibleFeeRate;

		return true;
	}

	private async Task<BuildTransactionResult?> HandleInsufficientBalanceWhenNormalAsync(Wallet wallet, TransactionInfo transactionInfo)
	{
		var dialog = new InsufficientBalanceDialogViewModel(transactionInfo.IsPrivate
			? BalanceType.Private
			: BalanceType.Pocket);

		var result = await NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);

		if (result.Result)
		{
			transactionInfo.SubtractFee = true;
			return await Task.Run(() => TransactionHelpers.BuildTransaction(wallet, transactionInfo, _destination));
		}

		if (wallet.Coins.TotalAmount() > transactionInfo.Amount)
		{
			var privacyControlDialogResult = await NavigateDialogAsync(
				new PrivacyControlViewModel(wallet, transactionInfo, _transaction?.SpentCoins, isSilent: false),
				NavigationTarget.DialogScreen);

			if (privacyControlDialogResult.Kind == DialogResultKind.Normal &&
				privacyControlDialogResult.Result is { })
			{
				transactionInfo.Coins = privacyControlDialogResult.Result;
			}

			return await BuildTransactionAsync();
		}

		Navigate().BackTo<SendViewModel>();
		return null;
	}

	private async Task<BuildTransactionResult?> HandleInsufficientBalanceWhenFixedAmountAsync(Wallet wallet,
		TransactionInfo transactionInfo)
	{
		if (wallet.Coins.TotalAmount() > transactionInfo.Amount)
		{
			await ShowErrorAsync("Transaction Building",
				$"There are not enough {(transactionInfo.IsPrivate ? "private funds" : "funds selected")} to cover the transaction fee",
				"Wasabi was unable to create your transaction.");

			var feeDialogResult = await NavigateDialogAsync(new SendFeeViewModel(wallet, transactionInfo, false),
				NavigationTarget.DialogScreen);
			if (feeDialogResult.Kind == DialogResultKind.Normal && feeDialogResult.Result is { } newFeeRate)
			{
				transactionInfo.FeeRate = newFeeRate;
			}

			if (TransactionHelpers.TryBuildTransaction(wallet, transactionInfo, _destination, out var txn))
			{
				return txn;
			}

			var privacyControlDialogResult = await NavigateDialogAsync(
				new PrivacyControlViewModel(wallet, transactionInfo, _transaction?.SpentCoins, isSilent: false),
				NavigationTarget.DialogScreen);
			if (privacyControlDialogResult.Kind == DialogResultKind.Normal &&
				privacyControlDialogResult.Result is { })
			{
				transactionInfo.Coins = privacyControlDialogResult.Result;
			}

			return await BuildTransactionAsync();
		}

		await ShowErrorAsync("Transaction Building",
			"There are not enough funds to cover the transaction fee",
			"Wasabi was unable to create your transaction.");

		Navigate().BackTo<SendViewModel>();
		return null;
	}

	private async Task<bool> NavigateConfirmLabelsDialogAsync(BuildTransactionResult transaction)
	{
		return (await NavigateDialogAsync(
			new ConfirmLabelsDialogViewModel(
				new PocketSuggestionViewModel(SmartLabel.Merge(
					transaction.SpentCoins.Select(
						x => x.GetLabels(_wallet.KeyManager.MinAnonScoreTarget))))),
			NavigationTarget.CompactDialogScreen)).Result;
	}

	private async Task InitialiseViewModelAsync()
	{
		if (await BuildTransactionAsync() is { } initialTransaction)
		{
			UpdateTransaction(CurrentTransactionSummary, initialTransaction);

			var suggestionTask = PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, _destination, initialTransaction, _isFixedAmount, _cancellationTokenSource.Token);

			if (CurrentTransactionSummary.TransactionHasPockets && !await NavigateConfirmLabelsDialogAsync(initialTransaction))
			{
				await OnChangePocketsAsync();
			}

			await suggestionTask;
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
			_info.ChangelessCoins = Enumerable.Empty<SmartCoin>();  // Clear ChangelessCoins on cancel, so the user can undo the optimization.
		}

		base.OnNavigatedFrom(isInHistory);

		DisplayedTransactionSummary = null;
	}

	private async Task OnConfirmAsync()
	{
		_cancellationTokenSource.Cancel();
		var labelDialog = new LabelEntryDialogViewModel(_wallet, _info);

		Navigate(NavigationTarget.CompactDialogScreen).To(labelDialog);

		var result = await labelDialog.GetDialogResultAsync();

		if (result.Result is null)
		{
			Navigate(NavigationTarget.CompactDialogScreen).Back(); // manually close the LabelEntryDialog when user cancels it. TODO: refactor.
			return;
		}

		_info.UserLabels = result.Result;

		var transaction = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, _info, _destination));
		var transactionAuthorizationInfo = new TransactionAuthorizationInfo(transaction);
		var authResult = await AuthorizeAsync(transactionAuthorizationInfo);
		if (authResult)
		{
			Navigate(NavigationTarget.CompactDialogScreen).Back(); // manually close the LabelEntryDialog when the authorization dialog never popped (empty password case). TODO: refactor.

			IsBusy = true;

			try
			{
				var finalTransaction =
					await GetFinalTransactionAsync(transactionAuthorizationInfo.Transaction, _info);
				await SendTransactionAsync(finalTransaction);
				Navigate().To(new SendSuccessViewModel(_wallet, finalTransaction));
			}
			catch (Exception ex)
			{
				await ShowErrorAsync("Transaction", ex.ToUserFriendlyString(),
					"Wasabi was unable to send your transaction.");
			}

			IsBusy = false;
		}
	}

	private async Task<bool> AuthorizeAsync(TransactionAuthorizationInfo transactionAuthorizationInfo)
	{
		if (!_wallet.KeyManager.IsHardwareWallet &&
			string.IsNullOrEmpty(_wallet.Kitchen.SaltSoup())) // Do not show auth dialog when password is empty
		{
			return true;
		}

		var authDialog = AuthorizationHelpers.GetAuthorizationDialog(_wallet, transactionAuthorizationInfo);
		var authDialogResult = await NavigateDialogAsync(authDialog, authDialog.DefaultTarget, NavigationMode.Clear);

		if (!authDialogResult.Result && authDialogResult.Kind == DialogResultKind.Normal)
		{
			await ShowErrorAsync("Authorization", "The Authorization has failed, please try again.", "");
		}

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
}
