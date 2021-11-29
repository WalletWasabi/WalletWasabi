using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public class SuggestionViewModel : ViewModelBase
	{
		public string Suggestion { get; set; }

		public BuildTransactionResult TransactionResult { get; protected set; }
	}

	public partial class ChangeAvoidanceSuggestionViewModel : SuggestionViewModel
	{
		[AutoNotify] private string _amount;
		[AutoNotify] private string _amountFiat;
		[AutoNotify] private List<PrivacySuggestionBenefit> _benefits;
		[AutoNotify] private PrivacyOptimisationLevel _optimisationLevel;
		[AutoNotify] private bool _optimisationLevelGood;

		public ChangeAvoidanceSuggestionViewModel(decimal originalAmount,
			BuildTransactionResult transactionResult,
			PrivacyOptimisationLevel optimisationLevel,
			decimal fiatExchangeRate,
			params PrivacySuggestionBenefit[] benefits)
		{
			TransactionResult = transactionResult;
			_optimisationLevel = optimisationLevel;
			_benefits = benefits.ToList();

			decimal total = transactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

			var fiatTotal = total * fiatExchangeRate;

			_amountFiat = total.GenerateFiatText(fiatExchangeRate, "USD");
			_optimisationLevelGood = optimisationLevel == PrivacyOptimisationLevel.Better;

			if (_optimisationLevelGood)
			{
				var fiatOriginal = originalAmount * fiatExchangeRate;
				var fiatDifference = fiatTotal - fiatOriginal;

				var difference = (fiatDifference > 0
						? $"{fiatDifference.GenerateFiatText("USD")} More"
						: $"{Math.Abs(fiatDifference).GenerateFiatText("USD")} Less")
					.Replace("(", "").Replace(")", "");

				_benefits.Add(new(false, difference));
			}
			else
			{
				// This is just to pad the control.
				_benefits.Add(new(false, " "));
			}

			_amount = $"{total}";
		}



		private static IEnumerable<ChangeAvoidanceSuggestionViewModel> NormalizeSuggestions(
			IEnumerable<ChangeAvoidanceSuggestionViewModel> suggestions, ChangeAvoidanceSuggestionViewModel defaultSuggestion)
		{
			var normalized = suggestions
				.OrderBy(x => x.TransactionResult.CalculateDestinationAmount())
				.ToList();

			if (normalized.Count == 3)
			{
				var index = normalized.IndexOf(defaultSuggestion);

				switch (index)
				{
					case 1:
						break;

					case 0:
						normalized = normalized.Take(2).ToList();
						break;

					case 2:
						normalized = normalized.Skip(1).ToList();
						break;
				}
			}

			return normalized;
		}

		public static async Task<(ChangeAvoidanceSuggestionViewModel preSelected, IEnumerable<ChangeAvoidanceSuggestionViewModel> items)> GenerateSuggestions(
			TransactionInfo transactionInfo, Wallet wallet, BuildTransactionResult requestedTransaction)
		{
			var intent = new PaymentIntent(
				transactionInfo.Address,
				MoneyRequest.CreateAllRemaining(subtractFee: true),
				transactionInfo.UserLabels);

			ChangeAvoidanceSuggestionViewModel? smallerSuggestion = null;

			if (requestedTransaction.SpentCoins.Count() > 1)
			{
				var smallerTransaction = await Task.Run(() => wallet.BuildTransaction(
					wallet.Kitchen.SaltSoup(),
					intent,
					FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
					allowUnconfirmed: true,
					requestedTransaction
						.SpentCoins
						.OrderBy(x => x.Amount)
						.Skip(1)
						.Select(x => x.OutPoint)));

				smallerSuggestion = new ChangeAvoidanceSuggestionViewModel(
					transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), smallerTransaction,
					PrivacyOptimisationLevel.Better, wallet.Synchronizer.UsdExchangeRate,
					new PrivacySuggestionBenefit(true, "Improved Privacy"));
			}

			var defaultSelection = new ChangeAvoidanceSuggestionViewModel(
				transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), requestedTransaction,
				PrivacyOptimisationLevel.Standard, wallet.Synchronizer.UsdExchangeRate,
				new PrivacySuggestionBenefit(false, "As Requested"));

			var largerTransaction = await Task.Run(() => wallet.BuildTransaction(
				wallet.Kitchen.SaltSoup(),
				intent,
				FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
				true,
				requestedTransaction.SpentCoins.Select(x => x.OutPoint)));

			var largerSuggestion = new ChangeAvoidanceSuggestionViewModel(
				transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), largerTransaction,
				PrivacyOptimisationLevel.Better, wallet.Synchronizer.UsdExchangeRate,
				new PrivacySuggestionBenefit(true, "Improved Privacy"));

			// There are several scenarios, both the alternate suggestions are <, or >, or 1 < and 1 >.
			// We sort them and add the suggestions accordingly.
			var suggestions = new List<ChangeAvoidanceSuggestionViewModel> {defaultSelection, largerSuggestion};

			if (smallerSuggestion is { })
			{
				suggestions.Add(smallerSuggestion);
			}

			var results = new List<ChangeAvoidanceSuggestionViewModel>();

			foreach (var suggestion in NormalizeSuggestions(suggestions, defaultSelection))
			{
				results.Add(suggestion);
			}

			return (defaultSelection, results);
		}
	}

	public partial class PrivacySuggestionsFlyoutViewModel : ViewModelBase
	{
		[AutoNotify] private SuggestionViewModel? _previewSuggestion;
		[AutoNotify] private SuggestionViewModel? _selectedSuggestion;
		[AutoNotify] private bool _isOpen;

		public PrivacySuggestionsFlyoutViewModel()
		{
			Suggestions = new ObservableCollection<SuggestionViewModel>();
		}

		public ObservableCollection<SuggestionViewModel> Suggestions { get; }

	}

	[NavigationMetaData(Title = "Transaction Preview")]
	public partial class TransactionPreviewViewModel : RoutableViewModel
	{
		private readonly Wallet _wallet;
		private readonly TransactionInfo _info;
		private BuildTransactionResult? _transaction;

		[AutoNotify] private string _confirmationTimeText = "";
		[AutoNotify] private string _feeText = "";
		[AutoNotify] private string _nextButtonText;
		[AutoNotify] private SmartLabel _labels;
		[AutoNotify] private string _amountText = "";
		[AutoNotify] private bool _transactionHasChange;
		[AutoNotify] private bool _transactionHasPockets;
		[AutoNotify] private bool _adjustFeeAvailable;
		[AutoNotify] private bool _maxPrivacy;
		[AutoNotify] private bool _issuesTooltip;

		public TransactionPreviewViewModel(Wallet wallet, TransactionInfo info)
		{
			_wallet = wallet;
			_labels = SmartLabel.Empty;
			_info = info;

			PrivacySuggestions = new PrivacySuggestionsFlyoutViewModel();

			PrivacySuggestions.WhenAnyValue(x => x.PreviewSuggestion)
				.Subscribe(x =>
				{
					Console.WriteLine(x.TransactionResult.CalculateDestinationAmount());
				});

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: false);
			EnableBack = true;

			AddressText = info.Address.ToString();
			PayJoinUrl = info.PayJoinClient?.PaymentUrl.AbsoluteUri;
			IsPayJoin = PayJoinUrl is not null;
			AdjustFeeAvailable = !TransactionFeeHelper.AreTransactionFeesEqual(_wallet);

			this.WhenAnyValue(x => x.TransactionHasChange, x => x.TransactionHasPockets)
				.Subscribe(_ =>
				{
					MaxPrivacy = !TransactionHasPockets && !TransactionHasChange;
				});

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

			AvoidChangeCommand = ReactiveCommand.CreateFromTask(OnAvoidChangeAsync);

			ChangePocketsCommand = ReactiveCommand.CreateFromTask(OnChangePocketsAsync);
		}

		public PrivacySuggestionsFlyoutViewModel PrivacySuggestions { get; }

		public bool PreferPsbtWorkflow => _wallet.KeyManager.PreferPsbtWorkflow;

		public string AddressText { get; }

		public string? PayJoinUrl { get; }

		public bool IsPayJoin { get; }

		public ICommand AdjustFeeCommand { get; }

		public ICommand AvoidChangeCommand { get; }

		public ICommand ChangePocketsCommand { get; }

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

		private async Task OnAdjustFeeAsync()
		{
			var feeRateDialogResult = await NavigateDialogAsync(new SendFeeViewModel(_wallet, _info, false));

			if (feeRateDialogResult.Kind == DialogResultKind.Normal && feeRateDialogResult.Result is { } newFeeRate && newFeeRate != _info.FeeRate)
			{
				_info.FeeRate = feeRateDialogResult.Result;

				var newTransaction = await BuildTransactionAsync();

				if (newTransaction is { })
				{
					UpdateTransaction(newTransaction);
				}
			}
		}

		private async Task OnAvoidChangeAsync()
		{
			var optimisePrivacyDialog =
				await NavigateDialogAsync(new OptimisePrivacyViewModel(_wallet, _info, _transaction!));

			if (optimisePrivacyDialog.Kind == DialogResultKind.Normal && optimisePrivacyDialog.Result is { })
			{
				UpdateTransaction(optimisePrivacyDialog.Result);
			}
		}

		private async Task OnChangePocketsAsync()
		{
			var selectPocketsDialog =
				await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, false));

			if (selectPocketsDialog.Kind == DialogResultKind.Normal && selectPocketsDialog.Result is { })
			{
				_info.Coins = selectPocketsDialog.Result;

				var newTransaction = await BuildTransactionAsync();

				if (newTransaction is { })
				{
					UpdateTransaction(newTransaction);
				}
			}
		}

		private async Task<bool> InitialiseTransactionAsync()
		{
			if (!_info.Coins.Any())
			{
				var privacyControlDialogResult = await NavigateDialogAsync(new PrivacyControlViewModel(_wallet, _info, isSilent: true));
				if (privacyControlDialogResult.Kind == DialogResultKind.Normal && privacyControlDialogResult.Result is { } coins)
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
			try
			{
				IsBusy = true;

				return await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, _info));
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

		private async Task<BuildTransactionResult?> HandleInsufficientBalanceWhenNormalAsync(Wallet wallet, TransactionInfo transactionInfo)
		{
			var dialog = new InsufficientBalanceDialogViewModel(transactionInfo.IsPrivatePocketUsed ? BalanceType.Private : BalanceType.Pocket);
			var result = await NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);

			if (result.Result)
			{
				transactionInfo.SubtractFee = true;
				return await Task.Run(() => TransactionHelpers.BuildTransaction(wallet, transactionInfo));
			}

			if (wallet.Coins.TotalAmount() > transactionInfo.Amount)
			{
				var privacyControlDialogResult = await NavigateDialogAsync(new PrivacyControlViewModel(wallet, transactionInfo, isSilent: false), NavigationTarget.DialogScreen);

				if (privacyControlDialogResult.Kind == DialogResultKind.Normal && privacyControlDialogResult.Result is { })
				{
					transactionInfo.Coins = privacyControlDialogResult.Result;
				}

				return await BuildTransactionAsync();
			}

			Navigate().BackTo<SendViewModel>();
			return null;
		}

		private async Task<BuildTransactionResult?> HandleInsufficientBalanceWhenPayJoinAsync(Wallet wallet, TransactionInfo transactionInfo)
		{
			if (wallet.Coins.TotalAmount() > transactionInfo.Amount)
			{
				await ShowErrorAsync("Transaction Building",
					$"There are not enough {(transactionInfo.IsPrivatePocketUsed ? "private funds" : "funds selected")} to cover the transaction fee",
					"Wasabi was unable to create your transaction.");

				var feeDialogResult = await NavigateDialogAsync(new SendFeeViewModel(wallet, transactionInfo, false), NavigationTarget.DialogScreen);
				if (feeDialogResult.Kind == DialogResultKind.Normal && feeDialogResult.Result is { } newFeeRate)
				{
					transactionInfo.FeeRate = newFeeRate;
				}

				if (TransactionHelpers.TryBuildTransaction(wallet, transactionInfo, out var txn))
				{
					return txn;
				}

				var privacyControlDialogResult = await NavigateDialogAsync(new PrivacyControlViewModel(wallet, transactionInfo, isSilent: false), NavigationTarget.DialogScreen);
				if (privacyControlDialogResult.Kind == DialogResultKind.Normal && privacyControlDialogResult.Result is { })
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

		private void UpdateTransaction(BuildTransactionResult transactionResult)
		{
			_transaction = transactionResult;

			var destinationAmount = _transaction.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);
			var btcAmountText = $"{destinationAmount} bitcoins ";
			var fiatAmountText =
				destinationAmount.GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");
			AmountText = $"{btcAmountText}{fiatAmountText}";

			var fee = _transaction.Fee;
			var btcFeeText = $"{fee.ToDecimal(MoneyUnit.Satoshi)} sats ";
			var fiatFeeText = fee.ToDecimal(MoneyUnit.BTC)
				.GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");

			Labels = SmartLabel.Merge(_info.UserLabels, SmartLabel.Merge(transactionResult.SpentCoins.Select(x => x.GetLabels())));

			FeeText = $"{btcFeeText}{fiatFeeText}";

			TransactionHasChange = _transaction.InnerWalletOutputs.Any(x => x.ScriptPubKey != _info.Address.ScriptPubKey);

			if (TransactionHasChange)
			{
				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					var (selected, suggestions) = await ChangeAvoidanceSuggestionViewModel.GenerateSuggestions(_info, _wallet, _transaction);

					foreach (var suggestion in suggestions)
					{
						PrivacySuggestions.Suggestions.Add(suggestion);
					}

					PrivacySuggestions.SelectedSuggestion = selected;
				});
			}

			TransactionHasPockets = !_info.IsPrivatePocketUsed;
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			ConfirmationTimeText = $"Approximately {TextHelpers.TimeSpanToFriendlyString(_info.ConfirmationTimeSpan)} ";

			Observable
				.FromEventPattern(_wallet.FeeProvider, nameof(_wallet.FeeProvider.AllFeeEstimateChanged))
				.Subscribe(_ => AdjustFeeAvailable = !TransactionFeeHelper.AreTransactionFeesEqual(_wallet))
				.DisposeWith(disposables);

			if (!isInHistory)
			{
				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					if (await InitialiseTransactionAsync())
					{
						var initialTransaction = await BuildTransactionAsync();

						if (initialTransaction is { })
						{
							UpdateTransaction(initialTransaction);
						}
					}
					else
					{
						Navigate().Back();
					}
				});
			}
		}

		private async Task OnConfirmAsync()
		{
			if (_transaction is { })
			{
				var transaction = _transaction;

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

		private async Task SendTransactionAsync(SmartTransaction transaction)
		{
			await Services.TransactionBroadcaster.SendTransactionAsync(transaction);
		}

		private async Task<SmartTransaction> GetFinalTransactionAsync(SmartTransaction transaction, TransactionInfo transactionInfo)
		{
			if (transactionInfo.PayJoinClient is { })
			{
				try
				{
					var payJoinTransaction = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo, isPayJoin: true));
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
