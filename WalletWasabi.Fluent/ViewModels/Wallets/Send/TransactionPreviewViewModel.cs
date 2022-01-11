using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
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
	private readonly Wallet _wallet;
	private readonly TransactionInfo _info;
	private readonly BitcoinAddress _destination;
	private BuildTransactionResult? _transaction;
	[AutoNotify] private string _nextButtonText;
	[AutoNotify] private bool _adjustFeeAvailable;
	[AutoNotify] private TransactionSummaryViewModel? _displayedTransactionSummary;

	public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info, BitcoinAddress destination)
	{
		_wallet = wallet;
		_info = info;
		_destination = destination;

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
					UpdateTransaction(CurrentTransactionSummary, ca.TransactionResult);

					await PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, _destination, ca.TransactionResult);
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
		_transaction = transaction;

		summary.UpdateTransaction(_transaction);

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

			await PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, _destination, newTransaction);
		}
	}

	private async Task OnChangePocketsAsync()
	{
		var selectPocketsDialog =
			await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, false));

		if (selectPocketsDialog.Kind == DialogResultKind.Normal && selectPocketsDialog.Result is { })
		{
			_info.Coins = selectPocketsDialog.Result;

			await BuildAndUpdateAsync();
		}
	}

	private async Task<bool> InitialiseTransactionAsync()
	{
		if (!_info.Coins.Any())
		{
			var privacyControlDialogResult =
				await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, isSilent: true));
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

			return await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, _info, _destination));
		}
		catch (TransactionFeeOverpaymentException ex)
		{
			var result = TrySetMaximumPossibleFee(ex.PercentageOfOverpayment, _wallet, _info);

			if (!result)
			{
				await ShowErrorAsync("Transaction Building", "At the moment, it is not possible to select a transaction fee that is less than the payment amount. The transaction cannot be sent.",
					"Wasabi was unable to create your transaction.");

				return null;
			}

			return await BuildTransactionAsync();
		}
		catch (InsufficientBalanceException)
		{
			if (_info.IsPayJoin)
			{
				return await HandleInsufficientBalanceWhenPayJoinAsync(_wallet, _info);
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
				new PrivacyControlViewModel(wallet, transactionInfo, isSilent: false),
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

	private async Task<BuildTransactionResult?> HandleInsufficientBalanceWhenPayJoinAsync(Wallet wallet,
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
				new PrivacyControlViewModel(wallet, transactionInfo, isSilent: false),
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

	private async Task InitialiseViewModelAsync()
	{
		if (await BuildTransactionAsync() is { } initialTransaction)
		{
			UpdateTransaction(CurrentTransactionSummary, initialTransaction);

			await PrivacySuggestions.BuildPrivacySuggestionsAsync(_wallet, _info, _destination, initialTransaction);
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
		base.OnNavigatedFrom(isInHistory);

		DisplayedTransactionSummary = null;
	}

	private async Task OnConfirmAsync()
	{
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
