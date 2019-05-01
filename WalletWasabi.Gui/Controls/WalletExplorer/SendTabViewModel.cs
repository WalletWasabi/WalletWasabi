using Avalonia.Threading;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Models;
using WalletWasabi.KeyManagement;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private string _buildTransactionButtonText;
		private bool _isMax;
		private string _maxClear;
		private string _amount;
		private int _feeTarget;
		private int _minimumFeeTarget;
		private int _maximumFeeTarget;
		private string _confirmationExpectedText;
		private string _feeText;
		private decimal _usdFee;
		private Money _btcFee;
		private Money _satoshiPerByteFeeRate;
		private decimal _feePercentage;
		private decimal _usdExchangeRate;
		private Money _allSelectedAmount;
		private string _password;
		private string _address;
		private string _label;
		private string _labelToolTip;
		private string _feeToolTip;
		private string _amountWaterMarkText;
		private string _amountToolTip;
		private bool _isBusy;
		private bool _isHardwareBusy;
		private const string SendTransactionButtonTextString = "Send Transaction";
		private const string WaitingForHardwareWalletButtonTextString = "Waiting for Hardware Wallet...";
		private const string SendingTransactionButtonTextString = "Sending Transaction...";
		private const string BuildTransactionButtonTextString = "Build Transaction";
		private const string BuildingTransactionButtonTextString = "Building Transaction...";

		private int _caretIndex;
		private ObservableCollection<SuggestionViewModel> _suggestions;
		private FeeDisplayFormat _feeDisplayFormat;

		private bool IgnoreAmountChanges { get; set; }

		private FeeDisplayFormat FeeDisplayFormat
		{
			get => _feeDisplayFormat;
			set
			{
				_feeDisplayFormat = value;
				Global.UiConfig.FeeDisplayFormat = (int)value;
			}
		}

		public SendTabViewModel(WalletViewModel walletViewModel, bool isTransactionBuilder = false)
			: base(isTransactionBuilder ? "Build Transaction" : "Send", walletViewModel)
		{
			_suggestions = new ObservableCollection<SuggestionViewModel>();
			Label = "";
			AllSelectedAmount = Money.Zero;
			UsdExchangeRate = Global.Synchronizer.UsdExchangeRate;
			IsTransactionBuilder = isTransactionBuilder;
			SetAmountWatermarkAndToolTip(Money.Zero);

			CoinList = new CoinListViewModel();
			Observable.FromEventPattern(CoinList, nameof(CoinList.SelectionChanged)).Subscribe(_ => SetFeesAndTexts());
			Observable.FromEventPattern(CoinList, nameof(CoinList.DequeueCoinsPressed)).Subscribe(_ => OnCoinsListDequeueCoinsPressedAsync());

			BuildTransactionButtonText = IsTransactionBuilder ? BuildTransactionButtonTextString : SendTransactionButtonTextString;

			ResetMax();
			SetFeeTargetLimits();
			FeeTarget = Global.UiConfig.FeeTarget ?? MinimumFeeTarget;
			FeeDisplayFormat = (FeeDisplayFormat)(Enum.ToObject(typeof(FeeDisplayFormat), Global.UiConfig.FeeDisplayFormat) ?? FeeDisplayFormat.SatoshiPerByte);
			SetFeesAndTexts();

			this.WhenAnyValue(x => x.Amount).Subscribe(amount =>
			{
				if (!IgnoreAmountChanges)
				{
					IsMax = false;

					// Correct amount
					Regex digitsOnly = new Regex(@"[^\d,.]");
					string betterAmount = digitsOnly.Replace(amount, ""); // Make it digits , and . only.
					betterAmount = betterAmount.Replace(',', '.');
					int countBetterAmount = betterAmount.Count(x => x == '.');
					if (countBetterAmount > 1) // Don't enable typing two dots.
					{
						var index = betterAmount.IndexOf('.', betterAmount.IndexOf('.') + 1);
						if (index > 0)
						{
							betterAmount = betterAmount.Substring(0, index);
						}
					}
					var dotIndex = betterAmount.IndexOf('.');
					if (betterAmount.Length - dotIndex > 8) // Enable max 8 decimals.
					{
						betterAmount = betterAmount.Substring(0, dotIndex + 1 + 8);
					}

					if (betterAmount != amount)
					{
						Dispatcher.UIThread.PostLogException(() =>
						{
							Amount = betterAmount;
						});
					}
				}

				if (Money.TryParse(amount.TrimStart('~', ' '), out Money amountBtc))
				{
					SetAmountWatermarkAndToolTip(amountBtc);
				}
				else
				{
					SetAmountWatermarkAndToolTip(Money.Zero);
				}

				SetFeesAndTexts();
			});

			(this).WhenAnyValue(x => x.IsBusy).Subscribe(_ =>
			{
				SetSendText();
			});

			(this).WhenAnyValue(x => x.IsHardwareBusy).Subscribe(_ =>
			{
				SetSendText();
			});

			this.WhenAnyValue(x => x.Password).Subscribe(x =>
			{
				try
				{
					if (x.NotNullAndNotEmpty())
					{
						char lastChar = x.Last();
						if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
						{
							Password = x.TrimEnd('\r', '\n');
						}
					}
				}
				catch (Exception ex)
				{
					Logging.Logger.LogTrace(ex);
				}
			});

			this.WhenAnyValue(x => x.Label).Subscribe(x => UpdateSuggestions(x));

			this.WhenAnyValue(x => x.FeeTarget).Subscribe(_ => SetFeesAndTexts());

			this.WhenAnyValue(x => x.CaretIndex).Subscribe(_ =>
			{
				if (Label is null)
				{
					return;
				}

				if (CaretIndex != Label.Length)
				{
					CaretIndex = Label.Length;
				}
			});

			MaxCommand = ReactiveCommand.Create(SetMax);

			FeeRateCommand = ReactiveCommand.Create(ChangeFeeRateDisplay);

			BuildTransactionCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				const string buildingTransactionStatusText = "Building transaction...";
				const string signingTransactionStatusText = "Signing transaction...";
				const string broadcastingTransactionStatusText = "Broadcasting transaction...";
				try
				{
					IsBusy = true;
					MainWindowViewModel.Instance.StatusBar.AddStatus(buildingTransactionStatusText);

					Password = Guard.Correct(Password);
					Label = Label.Trim(',', ' ').Trim();
					if (!IsMax && string.IsNullOrWhiteSpace(Label))
					{
						SetWarningMessage("Label is required.");
						return;
					}

					var selectedCoinViewModels = CoinList.Coins.Where(cvm => cvm.IsSelected);
					var selectedCoinReferences = selectedCoinViewModels.Select(cvm => new TxoRef(cvm.Model.TransactionId, cvm.Model.Index)).ToList();

					if (!selectedCoinReferences.Any())
					{
						SetWarningMessage("No coins are selected to spend.");
						return;
					}

					BitcoinAddress address;
					try
					{
						address = BitcoinAddress.Create(Address.Trim(), Global.Network);
					}
					catch (FormatException)
					{
						SetWarningMessage("Invalid address.");
						return;
					}

					var script = address.ScriptPubKey;
					var amount = Money.Zero;
					if (!IsMax)
					{
						if (!Money.TryParse(Amount, out amount) || amount == Money.Zero)
						{
							SetWarningMessage($"Invalid amount.");
							return;
						}

						if (amount == selectedCoinViewModels.Sum(x => x.Amount))
						{
							SetWarningMessage("Looks like you want to spend a whole coin. Try Max button instead.");
							return;
						}
					}
					var label = Label;
					var operation = new WalletService.Operation(script, amount, label);

					const string dequeuingSelectedCoinsStatusText = "Dequeueing selected coins...";
					try
					{
						MainWindowViewModel.Instance.StatusBar.AddStatus(dequeuingSelectedCoinsStatusText);
						TxoRef[] toDequeue = selectedCoinViewModels.Where(x => x.CoinJoinInProgress).Select(x => x.Model.GetTxoRef()).ToArray();
						if (toDequeue != null && toDequeue.Any())
						{
							await Global.ChaumianClient.DequeueCoinsFromMixAsync(toDequeue, "Coin is used in a spending transaction built by the user.");
						}
					}
					catch
					{
						SetWarningMessage("Spending coins those are being actively mixed is not allowed.");
						return;
					}
					finally
					{
						MainWindowViewModel.Instance.StatusBar.RemoveStatus(dequeuingSelectedCoinsStatusText);
					}

					var result = await Task.Run(() => Global.WalletService.BuildTransaction(Password, new[] { operation }, FeeTarget, allowUnconfirmed: true, allowedInputs: selectedCoinReferences));

					if (IsTransactionBuilder)
					{
						var txviewer = IoC.Get<IShell>().Documents?.OfType<TransactionViewerViewModel>()?.FirstOrDefault(x => x.Wallet.Id == Wallet.Id);
						if (txviewer is null)
						{
							txviewer = new TransactionViewerViewModel(Wallet);
							IoC.Get<IShell>().AddDocument(txviewer);
						}
						IoC.Get<IShell>().Select(txviewer);

						txviewer.Update(result);

						TryResetInputsOnSuccess("Transaction is successfully built!");
						return;
					}

					MainWindowViewModel.Instance.StatusBar.AddStatus(signingTransactionStatusText);
					SmartTransaction signedTransaction = result.Transaction;

					if (IsHardwareWallet && !result.Signed) // If hardware but still has a privkey then it's password, then meh.
					{
						const string connectingToHardwareWalletStatusText = "Connecting to hardware wallet...";
						const string acquiringSignatureFromHardwareWalletStatusText = "Acquiring signature from hardware wallet...";
						PSBT signedPsbt = null;
						try
						{
							IsHardwareBusy = true;
							MainWindowViewModel.Instance.StatusBar.AddStatus(connectingToHardwareWalletStatusText);
							// If we have no hardware wallet info then try refresh it. If we failed, then tha's a problem.
							if (KeyManager.HardwareWalletInfo is null && !await TryRefreshHardwareWalletInfoAsync(KeyManager))
							{
								SetWarningMessage("Could not find hardware wallet. Make sure it's plugged in and you're logged in with your PIN.");
								return;
							}

							MainWindowViewModel.Instance.StatusBar.AddStatus(acquiringSignatureFromHardwareWalletStatusText);
							signedPsbt = await HwiProcessManager.SignTxAsync(KeyManager.HardwareWalletInfo, result.Psbt);
						}
						catch (IOException ex) when (ex.Message.Contains("device not found", StringComparison.OrdinalIgnoreCase)
													|| ex.Message.Contains("Invalid status 6f04", StringComparison.OrdinalIgnoreCase) // It comes when device asleep too.
													|| ex.Message.Contains("Device is asleep", StringComparison.OrdinalIgnoreCase))
						{
							MainWindowViewModel.Instance.StatusBar.AddStatus(connectingToHardwareWalletStatusText);
							// The user may changed USB port. Try again with new enumeration.
							if (!await TryRefreshHardwareWalletInfoAsync(KeyManager))
							{
								SetWarningMessage("Could not find hardware wallet. Make sure it's plugged in and you're logged in with your PIN.");
								return;
							}

							MainWindowViewModel.Instance.StatusBar.AddStatus(acquiringSignatureFromHardwareWalletStatusText);
							signedPsbt = await HwiProcessManager.SignTxAsync(KeyManager.HardwareWalletInfo, result.Psbt);
						}
						finally
						{
							MainWindowViewModel.Instance.StatusBar.RemoveStatus(connectingToHardwareWalletStatusText, acquiringSignatureFromHardwareWalletStatusText);
							IsHardwareBusy = false;
						}

						signedTransaction = signedPsbt.ExtractSmartTransaction(result.Transaction.Height);
					}

					MainWindowViewModel.Instance.StatusBar.AddStatus(broadcastingTransactionStatusText);
					await Task.Run(async () => await Global.WalletService.SendTransactionAsync(signedTransaction));

					TryResetInputsOnSuccess("Transaction is successfully sent!");
				}
				catch (InsufficientBalanceException ex)
				{
					Money needed = ex.Minimum - ex.Actual;
					SetWarningMessage($"Not enough coins selected. You need an estimated {needed.ToString(false, true)} BTC more to make this transaction.");
				}
				catch (Exception ex)
				{
					SetWarningMessage(ex.ToTypeMessageString());
				}
				finally
				{
					MainWindowViewModel.Instance.StatusBar.RemoveStatus(buildingTransactionStatusText, signingTransactionStatusText, broadcastingTransactionStatusText);
					IsBusy = false;
				}
			},
			this.WhenAny(x => x.IsMax, x => x.Amount, x => x.Address, x => x.IsBusy,
				(isMax, amount, address, busy) => (isMax.Value || !string.IsNullOrWhiteSpace(amount.Value)) && !string.IsNullOrWhiteSpace(Address) && !IsBusy));
		}

		private async Task<bool> TryRefreshHardwareWalletInfoAsync(KeyManager keyManager)
		{
			var hwis = await HwiProcessManager.EnumerateAsync();

			bool ledgerNotReady = hwis.Any(x => x.Type == HardwareWalletType.Ledger && !x.Ready);
			if (ledgerNotReady) // For Ledger you have to log into your "Bitcoin" account.
			{
				throw new InvalidOperationException("Log into your Bitcoin account on your Ledger. If you're already logged in, log out and log in again.");
			}

			var fingerprint = keyManager.MasterFingerprint;

			if (fingerprint is null) return false;

			keyManager.HardwareWalletInfo = hwis.FirstOrDefault(x => x.MasterFingerprint == fingerprint);

			return keyManager.HardwareWalletInfo != null;
		}

		private void SetSendText()
		{
			if (IsTransactionBuilder)
			{
				BuildTransactionButtonText = IsBusy ? BuildingTransactionButtonTextString : BuildTransactionButtonTextString;
				return;
			}

			if (IsHardwareBusy)
			{
				BuildTransactionButtonText = WaitingForHardwareWalletButtonTextString;
			}
			else if (IsBusy)
			{
				BuildTransactionButtonText = SendingTransactionButtonTextString;
			}
			else
			{
				BuildTransactionButtonText = SendTransactionButtonTextString;
			}
		}

		private void SetAmountWatermarkAndToolTip(Money amount)
		{
			if (amount == Money.Zero)
			{
				AmountWatermarkText = "Amount (BTC)";
			}
			else
			{
				long amountUsd = 0;
				try
				{
					amountUsd = (long)amount.ToUsd(UsdExchangeRate);
				}
				catch (OverflowException ex)
				{
					Logging.Logger.LogTrace<SendTabViewModel>(ex);
				}
				if (amountUsd != 0)
				{
					AmountWatermarkText = $"Amount (BTC) ~ ${amountUsd}";
				}
				else
				{
					AmountWatermarkText = "Amount (BTC)";
				}
			}

			AmountToolTip = $"Exchange Rate: {(long)UsdExchangeRate} BTC/USD.";
		}

		private void ChangeFeeRateDisplay()
		{
			var nextval = (from FeeDisplayFormat val in Enum.GetValues(typeof(FeeDisplayFormat))
						   where val > FeeDisplayFormat
						   orderby val
						   select val).DefaultIfEmpty().First();
			FeeDisplayFormat = nextval;
			SetFeesAndTexts();
		}

		private void SetFeesAndTexts()
		{
			AllFeeEstimate allFeeEstimate = Global.Synchronizer?.AllFeeEstimate;

			var feeTarget = FeeTarget;

			if (allFeeEstimate != null)
			{
				int prevKey = allFeeEstimate.Estimations.Keys.First();
				foreach (int target in allFeeEstimate.Estimations.Keys)
				{
					if (feeTarget == target)
					{
						feeTarget = target;
						break;
					}
					else if (feeTarget < target)
					{
						feeTarget = prevKey;
						break;
					}
					prevKey = target;
				}
			}

			if (feeTarget >= 2 && feeTarget <= 6) // minutes
			{
				ConfirmationExpectedText = $"{feeTarget}0 minutes";
			}
			else if (feeTarget >= 7 && feeTarget <= 144) // hours
			{
				var h = feeTarget / 6;
				ConfirmationExpectedText = $"{h} {IfPlural(h, "hour", "hours")}";
			}
			else if (feeTarget >= 145 && feeTarget < 1008) // days
			{
				var d = feeTarget / 144;
				ConfirmationExpectedText = $"{d} {IfPlural(d, "day", "days")}";
			}
			else if (feeTarget == 10008)
			{
				ConfirmationExpectedText = $"two weeks™";
			}

			if (allFeeEstimate != null)
			{
				SetFees(allFeeEstimate, feeTarget);

				switch (FeeDisplayFormat)
				{
					case FeeDisplayFormat.SatoshiPerByte:
						FeeText = $"(~ {SatoshiPerByteFeeRate.Satoshi} sat/byte)";
						FeeToolTip = "Expected fee rate in satoshi / vbyte.";
						break;

					case FeeDisplayFormat.USD:
						FeeText = $"(~ ${UsdFee.ToString("0.##")})";
						FeeToolTip = $"Estimated total fees in USD. Exchange Rate: {(long)UsdExchangeRate} BTC/USD.";
						break;

					case FeeDisplayFormat.BTC:
						FeeText = $"(~ {BtcFee.ToString(false, false)} BTC)";
						FeeToolTip = "Estimated total fees in BTC.";
						break;

					case FeeDisplayFormat.Percentage:
						FeeText = $"(~ {FeePercentage.ToString("0.#")} %)";
						FeeToolTip = "Expected percentage of fees against the amount to be sent.";
						break;

					default:
						throw new NotSupportedException("This is impossible.");
				}
			}

			SetAmountIfMax();
		}

		private static string IfPlural(int val, string singular, string plural)
		{
			if (val == 1)
			{
				return singular;
			}

			return plural;
		}

		private void SetAmountIfMax()
		{
			IgnoreAmountChanges = true;
			if (IsMax)
			{
				if (AllSelectedAmount == Money.Zero)
				{
					Amount = "No Coins Selected";
				}
				else
				{
					Amount = $"~ {AllSelectedAmount.ToString(false, true)}";
				}
			}

			IgnoreAmountChanges = false;
		}

		private void SetFees(AllFeeEstimate allFeeEstimate, int feeTarget)
		{
			SatoshiPerByteFeeRate = allFeeEstimate.GetFeeRate(feeTarget);

			IEnumerable<SmartCoin> selectedCoins = CoinList.Coins.Where(cvm => cvm.IsSelected).Select(x => x.Model);

			int vsize = 150;
			if (selectedCoins.Any())
			{
				if (IsMax)
				{
					vsize = NBitcoinHelpers.CalculateVsizeAssumeSegwit(selectedCoins.Count(), 1);
				}
				else
				{
					if (Money.TryParse(Amount.TrimStart('~', ' '), out Money amount))
					{
						var inNum = 0;
						var amountSoFar = Money.Zero;
						foreach (SmartCoin coin in selectedCoins.OrderByDescending(x => x.Amount))
						{
							amountSoFar += coin.Amount;
							inNum++;
							if (amountSoFar > amount)
							{
								break;
							}
						}
						vsize = NBitcoinHelpers.CalculateVsizeAssumeSegwit(inNum, 2);
					}
					// Else whatever, don't change.
				}
			}

			BtcFee = Money.Satoshis(vsize * SatoshiPerByteFeeRate);

			long all = selectedCoins.Sum(x => x.Amount);
			if (IsMax)
			{
				if (all != 0)
				{
					FeePercentage = 100 * (decimal)BtcFee.Satoshi / all;
				}
			}
			else
			{
				if (Money.TryParse(Amount.TrimStart('~', ' '), out Money amount) && amount.Satoshi != 0)
				{
					FeePercentage = 100 * (decimal)BtcFee.Satoshi / amount.Satoshi;
				}
			}

			if (UsdExchangeRate != 0)
			{
				UsdFee = BtcFee.ToUsd(UsdExchangeRate);
			}

			AllSelectedAmount = Math.Max(Money.Zero, all - BtcFee);
		}

		private void SetFeeTargetLimits()
		{
			var allFeeEstimate = Global.Synchronizer?.AllFeeEstimate;

			if (allFeeEstimate != null)
			{
				MinimumFeeTarget = allFeeEstimate.Estimations.Min(x => x.Key); // This should be always 2, but bugs will be seen at least if it isn't.
				MaximumFeeTarget = allFeeEstimate.Estimations.Max(x => x.Key);
			}
			else
			{
				MinimumFeeTarget = 2;
				MaximumFeeTarget = 1008;
			}
		}

		private void SetMax()
		{
			if (IsMax)
			{
				ResetMax();
				return;
			}

			IsMax = true;
			MaxClear = "Clear";

			SetAmountIfMax();

			LabelToolTip = "Spending whole coins doesn't generate change, thus labeling is unnecessary.";
		}

		private void ResetMax()
		{
			IsMax = false;
			MaxClear = "Max";

			IgnoreAmountChanges = true;
			Amount = "0.0";
			IgnoreAmountChanges = false;

			LabelToolTip = "Start labelling today and your privacy will thank you tomorrow!";
		}

		private void TryResetInputsOnSuccess(string successMessage)
		{
			try
			{
				ResetMax();
				Address = "";
				Label = "";
				Password = "";

				SetSuccessMessage(successMessage);
			}
			catch (Exception ex)
			{
				Logging.Logger.LogInfo<SendTabViewModel>(ex);
			}
		}

		public CoinListViewModel CoinList { get; }

		private async void OnCoinsListDequeueCoinsPressedAsync()
		{
			try
			{
				var selectedCoin = CoinList?.SelectedCoin;
				if (selectedCoin is null)
				{
					return;
				}

				await DoDequeueAsync(new[] { selectedCoin });
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<SendTabViewModel>(ex);
			}
		}

		private async Task DoDequeueAsync(IEnumerable<CoinViewModel> selectedCoins)
		{
			if (!selectedCoins.Any())
			{
				SetWarningMessage("No coins are selected to dequeue.");
				return;
			}
			else
			{
				SetWarningMessage("");
			}

			try
			{
				await Global.ChaumianClient.DequeueCoinsFromMixAsync(selectedCoins.Select(c => c.Model).ToArray(), "Dequeued by the user.");
			}
			catch (Exception ex)
			{
				Logging.Logger.LogWarning<CoinJoinTabViewModel>(ex);
				var builder = new StringBuilder(ex.ToTypeMessageString());
				if (ex is AggregateException aggex)
				{
					foreach (var iex in aggex.InnerExceptions)
					{
						builder.Append(Environment.NewLine + iex.ToTypeMessageString());
					}
				}
				SetWarningMessage(builder.ToString());
				return;
			}
		}

		public bool IsBusy
		{
			get => _isBusy;
			set => this.RaiseAndSetIfChanged(ref _isBusy, value);
		}

		public bool IsHardwareBusy
		{
			get => _isHardwareBusy;
			set => this.RaiseAndSetIfChanged(ref _isHardwareBusy, value);
		}

		public string BuildTransactionButtonText
		{
			get => _buildTransactionButtonText;
			set => this.RaiseAndSetIfChanged(ref _buildTransactionButtonText, value);
		}

		public bool IsMax
		{
			get => _isMax;
			set => this.RaiseAndSetIfChanged(ref _isMax, value);
		}

		public string MaxClear
		{
			get => _maxClear;
			set => this.RaiseAndSetIfChanged(ref _maxClear, value);
		}

		public string Amount
		{
			get => _amount;
			set => this.RaiseAndSetIfChanged(ref _amount, value);
		}

		public int FeeTarget
		{
			get => _feeTarget;
			set
			{
				this.RaiseAndSetIfChanged(ref _feeTarget, value);
				Global.UiConfig.FeeTarget = value;
			}
		}

		public int MinimumFeeTarget
		{
			get => _minimumFeeTarget;
			set => this.RaiseAndSetIfChanged(ref _minimumFeeTarget, value);
		}

		public int MaximumFeeTarget
		{
			get => _maximumFeeTarget;
			set => this.RaiseAndSetIfChanged(ref _maximumFeeTarget, value);
		}

		public string ConfirmationExpectedText
		{
			get => _confirmationExpectedText;
			set => this.RaiseAndSetIfChanged(ref _confirmationExpectedText, value);
		}

		public string FeeText
		{
			get => _feeText;
			set => this.RaiseAndSetIfChanged(ref _feeText, value);
		}

		public decimal UsdFee
		{
			get => _usdFee;
			set => this.RaiseAndSetIfChanged(ref _usdFee, value);
		}

		public Money BtcFee
		{
			get => _btcFee;
			set => this.RaiseAndSetIfChanged(ref _btcFee, value);
		}

		public Money SatoshiPerByteFeeRate
		{
			get => _satoshiPerByteFeeRate;
			set => this.RaiseAndSetIfChanged(ref _satoshiPerByteFeeRate, value);
		}

		public decimal FeePercentage
		{
			get => _feePercentage;
			set => this.RaiseAndSetIfChanged(ref _feePercentage, value);
		}

		public decimal UsdExchangeRate
		{
			get => _usdExchangeRate;
			set => this.RaiseAndSetIfChanged(ref _usdExchangeRate, value);
		}

		public Money AllSelectedAmount
		{
			get => _allSelectedAmount;
			set => this.RaiseAndSetIfChanged(ref _allSelectedAmount, value);
		}

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public int CaretIndex
		{
			get => _caretIndex;
			set => this.RaiseAndSetIfChanged(ref _caretIndex, value);
		}

		public ObservableCollection<SuggestionViewModel> Suggestions
		{
			get => _suggestions;
			set => this.RaiseAndSetIfChanged(ref _suggestions, value);
		}

		private void UpdateSuggestions(string words)
		{
			if (string.IsNullOrWhiteSpace(words))
			{
				Suggestions?.Clear();
				return;
			}

			var enteredWordList = words.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
			var lastWord = enteredWordList?.LastOrDefault()?.Replace("\t", "") ?? "";

			if (!lastWord.Any())
			{
				Suggestions.Clear();
				return;
			}

			string[] nonSpecialLabels = Global.WalletService.GetNonSpecialLabels().ToArray();
			IEnumerable<string> suggestedWords = nonSpecialLabels.Where(w => w.StartsWith(lastWord, StringComparison.InvariantCultureIgnoreCase))
				.Union(nonSpecialLabels.Where(w => w.Contains(lastWord, StringComparison.InvariantCultureIgnoreCase)))
				.Except(enteredWordList)
				.Take(3);

			Suggestions.Clear();
			foreach (var suggestion in suggestedWords)
			{
				Suggestions.Add(new SuggestionViewModel(suggestion, OnAddWord));
			}
		}

		public void OnAddWord(string word)
		{
			var words = Label.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
			if (words.Length == 0)
			{
				Label = word + ", ";
			}
			else
			{
				words[words.Length - 1] = word;
				Label = string.Join(", ", words) + ", ";
			}

			CaretIndex = Label.Length;

			Suggestions.Clear();
		}

		public string ValidateAddress()
		{
			if (string.IsNullOrEmpty(Address))
			{
				return "";
			}

			if (!string.IsNullOrWhiteSpace(Address))
			{
				var trimmed = Address.Trim();
				try
				{
					BitcoinAddress.Create(trimmed, Global.Network);
					return "";
				}
				catch
				{
				}
			}

			return $"Invalid {nameof(Address)}";
		}

		[ValidateMethod(nameof(ValidateAddress))]
		public string Address
		{
			get => _address;
			set => this.RaiseAndSetIfChanged(ref _address, value);
		}

		public string Label
		{
			get => _label;
			set => this.RaiseAndSetIfChanged(ref _label, value);
		}

		public string LabelToolTip
		{
			get => _labelToolTip;
			set => this.RaiseAndSetIfChanged(ref _labelToolTip, value);
		}

		public string FeeToolTip
		{
			get => _feeToolTip;
			set => this.RaiseAndSetIfChanged(ref _feeToolTip, value);
		}

		public string AmountWatermarkText
		{
			get => _amountWaterMarkText;
			set => this.RaiseAndSetIfChanged(ref _amountWaterMarkText, value);
		}

		public string AmountToolTip
		{
			get => _amountToolTip;
			set => this.RaiseAndSetIfChanged(ref _amountToolTip, value);
		}

		public ReactiveCommand<Unit, Unit> BuildTransactionCommand { get; }

		public ReactiveCommand<Unit, Unit> MaxCommand { get; }

		public ReactiveCommand<Unit, Unit> FeeRateCommand { get; }
		public bool IsTransactionBuilder { get; }

		public override void OnOpen()
		{
			if (Disposables != null)
			{
				throw new Exception("Send tab opened before last one closed.");
			}

			Disposables = new CompositeDisposable();

			Global.Synchronizer.WhenAnyValue(x => x.AllFeeEstimate).Subscribe(_ =>
			{
				SetFeeTargetLimits();

				if (FeeTarget < MinimumFeeTarget) // Should never happen.
				{
					FeeTarget = MinimumFeeTarget;
				}
				else if (FeeTarget > MaximumFeeTarget)
				{
					FeeTarget = MaximumFeeTarget;
				}

				SetFeesAndTexts();
			}).DisposeWith(Disposables);

			Global.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Subscribe(_ =>
			{
				var exchangeRate = Global.Synchronizer.UsdExchangeRate;

				if (exchangeRate != 0)
				{
					UsdExchangeRate = exchangeRate;
				}

				SetFeesAndTexts();
			}).DisposeWith(Disposables);

			CoinList.OnOpen();

			base.OnOpen();
		}

		public override bool OnClose()
		{
			Disposables.Dispose();

			Disposables = null;

			CoinList.OnClose();

			return base.OnClose();
		}
	}
}
