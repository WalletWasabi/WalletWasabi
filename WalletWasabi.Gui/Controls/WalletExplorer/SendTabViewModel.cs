using Avalonia.Input;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Gui.ViewModels.Validation;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Hwi.Models;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Models.TransactionBuilding;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private string _buildTransactionButtonText;
		private bool _isMax;
		private string _amountText;
		private string _userFeeText;
		private int _feeTarget;
		private int _minimumFeeTarget;
		private int _maximumFeeTarget;
		private ObservableAsPropertyHelper<bool> _minMaxFeeTargetsEqual;
		private string _feeText;
		private decimal _usdFee;
		private Money _estimatedBtcFee;
		private FeeRate _feeRate;
		private decimal _feePercentage;
		private ObservableAsPropertyHelper<decimal> _usdExchangeRate;
		private Money _allSelectedAmount;
		private string _password;
		private string _address;
		private string _label;
		private string _labelToolTip;
		private string _feeToolTip;
		private string _amountWaterMarkText;
		private bool _isBusy;
		private bool _isHardwareBusy;
		private int _caretIndex;
		private ObservableAsPropertyHelper<bool> _isCustomFee;

		private const string SendTransactionButtonTextString = "Send Transaction";
		private const string WaitingForHardwareWalletButtonTextString = "Waiting for Hardware Wallet...";
		private const string SendingTransactionButtonTextString = "Sending Transaction...";
		private const string BuildTransactionButtonTextString = "Build Transaction";
		private const string BuildingTransactionButtonTextString = "Building Transaction...";

		private ObservableCollection<SuggestionViewModel> _suggestions;
		private FeeDisplayFormat _feeDisplayFormat;
		private bool _isSliderFeeUsed = true;
		private double _feeControlOpacity;

		private FeeDisplayFormat FeeDisplayFormat
		{
			get => _feeDisplayFormat;
			set
			{
				_feeDisplayFormat = value;
				Global.UiConfig.FeeDisplayFormat = (int)value;
			}
		}

		private void ResetUi()
		{
			Suggestions = new ObservableCollection<SuggestionViewModel>();
			Address = "";
			Label = "";
			Password = "";
			AllSelectedAmount = Money.Zero;
			IsMax = false;
			LabelToolTip = "Start labeling today and your privacy will thank you tomorrow!";
			AmountText = "0.0";
			UserFeeText = "1";
		}

		public SendTabViewModel(WalletViewModel walletViewModel, bool isTransactionBuilder = false)
			: base(isTransactionBuilder ? "Build Transaction" : "Send", walletViewModel)
		{
			IsTransactionBuilder = isTransactionBuilder;
			BuildTransactionButtonText = IsTransactionBuilder ? BuildTransactionButtonTextString : SendTransactionButtonTextString;

			ResetUi();
			SetAmountWatermark(Money.Zero);

			CoinList = new CoinListViewModel(Global, CoinListContainerType.SendTabViewModel);

			Observable.FromEventPattern(CoinList, nameof(CoinList.SelectionChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => SetFeesAndTexts());

			Observable.FromEventPattern(CoinList, nameof(CoinList.DequeueCoinsPressed))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => OnCoinsListDequeueCoinsPressedAsync());

			_minMaxFeeTargetsEqual = this.WhenAnyValue(x => x.MinimumFeeTarget, x => x.MaximumFeeTarget, (x, y) => x == y)
				.ToProperty(this, x => x.MinMaxFeeTargetsEqual, scheduler: RxApp.MainThreadScheduler);

			SetFeeTargetLimits();
			FeeTarget = Global.UiConfig.FeeTarget;
			FeeDisplayFormat = (FeeDisplayFormat)(Enum.ToObject(typeof(FeeDisplayFormat), Global.UiConfig.FeeDisplayFormat) ?? FeeDisplayFormat.SatoshiPerByte);
			SetFeesAndTexts();

			this.WhenAnyValue(x => x.AmountText)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(amount =>
				{
					if (!IsMax)
					{
						// Correct amount
						Regex digitsOnly = new Regex(@"[^\d,.]");
						string betterAmount = digitsOnly.Replace(amount, ""); // Make it digits , and . only.

						betterAmount = betterAmount.Replace(',', '.');
						int countBetterAmount = betterAmount.Count(x => x == '.');
						if (countBetterAmount > 1) // Do not enable typing two dots.
						{
							var index = betterAmount.IndexOf('.', betterAmount.IndexOf('.') + 1);
							if (index > 0)
							{
								betterAmount = betterAmount.Substring(0, index);
							}
						}
						var dotIndex = betterAmount.IndexOf('.');
						if (dotIndex != -1 && betterAmount.Length - dotIndex > 8) // Enable max 8 decimals.
						{
							betterAmount = betterAmount.Substring(0, dotIndex + 1 + 8);
						}

						if (betterAmount != amount)
						{
							AmountText = betterAmount;
						}
					}

					if (Money.TryParse(amount.TrimStart('~', ' '), out Money amountBtc))
					{
						SetAmountWatermark(amountBtc);
					}
					else
					{
						SetAmountWatermark(Money.Zero);
					}

					SetFeesAndTexts();
				});

			this.WhenAnyValue(x => x.IsBusy)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => SetSendText());

			this.WhenAnyValue(x => x.IsHardwareBusy)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => SetSendText());

			this.WhenAnyValue(x => x.Label)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(UpdateSuggestions);

			this.WhenAnyValue(x => x.FeeTarget)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					IsSliderFeeUsed = true;
					SetFeesAndTexts();
				});

			this.WhenAnyValue(x => x.IsSliderFeeUsed)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(enabled => FeeControlOpacity = enabled ? 1 : 0.5); // Give the control the disabled feeling. Real Disable it not a solution as we have to detect if the slider is moved.

#pragma warning disable IDE0053 // Use expression body for lambda expressions
			MaxCommand = ReactiveCommand.Create(() => { IsMax = !IsMax; }, outputScheduler: RxApp.MainThreadScheduler);
#pragma warning restore IDE0053 // Use expression body for lambda expressions

			this.WhenAnyValue(x => x.IsMax)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					if (IsMax)
					{
						SetAmountIfMax();

						LabelToolTip = "Spending whole coins does not generate change, thus labeling is unnecessary.";
					}
					else
					{
						AmountText = "0.0";

						LabelToolTip = "Start labeling today and your privacy will thank you tomorrow!";
					}
				});

			FeeRateCommand = ReactiveCommand.Create(ChangeFeeRateDisplay, outputScheduler: RxApp.MainThreadScheduler);

			OnAddressPasteCommand = ReactiveCommand.Create((BitcoinUrlBuilder url) =>
			{
				var label = new SmartLabel(url.Label);
				if (!label.IsEmpty)
				{
					Label = label.ToString();
				}

				if (url.Amount != null)
				{
					AmountText = url.Amount.ToString(false, true);
				}
			});

			BuildTransactionCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				bool isCompatibilityPasswordUsed = false;
				try
				{
					IsBusy = true;
					MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusBarStatus.BuildingTransaction);

					var label = new SmartLabel(Label);
					Label = label.ToString();
					if (!IsMax && label.IsEmpty)
					{
						SetWarningMessage($"{nameof(Label)} is required.");
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

					MoneyRequest moneyRequest;
					if (IsMax)
					{
						moneyRequest = MoneyRequest.CreateAllRemaining();
					}
					else
					{
						if (!Money.TryParse(AmountText, out Money amount) || amount == Money.Zero)
						{
							SetWarningMessage("Invalid amount.");
							return;
						}

						if (amount == selectedCoinViewModels.Sum(x => x.Amount))
						{
							SetWarningMessage("Looks like you want to spend a whole coin. Try Max button instead.");
							return;
						}
						moneyRequest = MoneyRequest.Create(amount);
					}

					if (FeeRate is null || FeeRate.SatoshiPerByte < 1)
					{
						SetWarningMessage("Invalid fee rate, must be greater than or equal to one.");
						return;
					}

					bool useCustomFee = !IsSliderFeeUsed;
					var feeStrategy = FeeStrategy.CreateFromFeeRate(FeeRate);

					var intent = new PaymentIntent(address, moneyRequest, label);
					try
					{
						MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusBarStatus.DequeuingSelectedCoins);
						TxoRef[] toDequeue = selectedCoinViewModels.Where(x => x.CoinJoinInProgress).Select(x => x.Model.GetTxoRef()).ToArray();
						if (toDequeue != null && toDequeue.Any())
						{
							await Global.ChaumianClient.DequeueCoinsFromMixAsync(toDequeue, "Coin is used in a spending transaction built by the user.");
						}
					}
					catch
					{
						SetWarningMessage("Spending coins that are being actively mixed is not allowed.");
						return;
					}
					finally
					{
						MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusBarStatus.DequeuingSelectedCoins);
					}

					if (!KeyManager.IsWatchOnly)
					{
						try
						{
							PasswordHelper.GetMasterExtKey(KeyManager, Password, out string compatiblityPasswordUsed); // We could use TryPassword but we need the exception.
							if (compatiblityPasswordUsed != null)
							{
								isCompatibilityPasswordUsed = true;
								Password = compatiblityPasswordUsed; // Overwrite the password for BuildTransaction function.
							}
						}
						catch (Exception ex)
						{
							SetWarningMessage(ex.ToTypeMessageString());
							return;
						}
					}

					BuildTransactionResult result = await Task.Run(() => Global.WalletService.BuildTransaction(Password, intent, feeStrategy, allowUnconfirmed: true, allowedInputs: selectedCoinReferences));

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

						if (isCompatibilityPasswordUsed)
						{
							WarningMessage = PasswordHelper.CompatibilityPasswordWarnMessage;
						}
						return;
					}

					MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusBarStatus.SigningTransaction);
					SmartTransaction signedTransaction = result.Transaction;

					if (IsHardwareWallet && !result.Signed) // If hardware but still has a privkey then it's password, then meh.
					{
						try
						{
							IsHardwareBusy = true;
							MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusBarStatus.AcquiringSignatureFromHardwareWallet);
							var client = new HwiClient(Global.Network);

							using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
							PSBT signedPsbt = null;
							try
							{
								signedPsbt = await client.SignTxAsync(KeyManager.MasterFingerprint.Value, result.Psbt, cts.Token);
							}
							catch (HwiException)
							{
								await PinPadViewModel.UnlockAsync(Global);
								signedPsbt = await client.SignTxAsync(KeyManager.MasterFingerprint.Value, result.Psbt, cts.Token);
							}
							signedTransaction = signedPsbt.ExtractSmartTransaction(result.Transaction);
						}
						catch (Exception ex)
						{
							SetWarningMessage(ex.ToTypeMessageString());
							return;
						}
						finally
						{
							MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusBarStatus.AcquiringSignatureFromHardwareWallet);
							IsHardwareBusy = false;
						}
					}

					MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusBarStatus.BroadcastingTransaction);
					await Task.Run(async () => await Global.WalletService.SendTransactionAsync(signedTransaction));

					TryResetInputsOnSuccess("Transaction is successfully sent!");

					if (isCompatibilityPasswordUsed)
					{
						WarningMessage = PasswordHelper.CompatibilityPasswordWarnMessage;
					}
				}
				catch (InsufficientBalanceException ex)
				{
					Money needed = ex.Minimum - ex.Actual;
					SetWarningMessage($"Not enough coins selected. You need an estimated {needed.ToString(false, true)} BTC more to make this transaction.");
				}
				catch (HttpRequestException ex)
				{
					SetWarningMessage(ex.ToUserFriendlyString());
				}
				catch (Exception ex)
				{
					SetWarningMessage(ex.ToTypeMessageString());
				}
				finally
				{
					MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusBarStatus.BuildingTransaction, StatusBarStatus.SigningTransaction, StatusBarStatus.BroadcastingTransaction);
					IsBusy = false;
				}
			},
			this.WhenAny(x => x.IsMax, x => x.AmountText, x => x.Address, x => x.IsBusy,
				(isMax, amount, address, busy) => (isMax.Value || !string.IsNullOrWhiteSpace(amount.Value)) && !string.IsNullOrWhiteSpace(Address) && !IsBusy)
				.ObserveOn(RxApp.MainThreadScheduler));

			UserFeeTextKeyUpCommand = ReactiveCommand.Create((KeyEventArgs key) =>
			{
				IsSliderFeeUsed = string.IsNullOrEmpty(UserFeeText);
				SetFeesAndTexts();
			});

#pragma warning disable IDE0053 // Use expression body for lambda expressions
			FeeSliderClickedCommand = ReactiveCommand.Create((PointerPressedEventArgs mouse) => { IsSliderFeeUsed = true; });
#pragma warning restore IDE0053 // Use expression body for lambda expressions

			HighLightFeeSliderCommand = ReactiveCommand.Create((bool entered) =>
			{
				if (IsSliderFeeUsed)
				{
					return;
				}

				FeeControlOpacity = entered ? 0.8 : 0.5;
			});

			Observable
				.Merge(MaxCommand.ThrownExceptions)
				.Merge(FeeRateCommand.ThrownExceptions)
				.Merge(OnAddressPasteCommand.ThrownExceptions)
				.Merge(BuildTransactionCommand.ThrownExceptions)
				.Merge(UserFeeTextKeyUpCommand.ThrownExceptions)
				.Merge(FeeSliderClickedCommand.ThrownExceptions)
				.Merge(HighLightFeeSliderCommand.ThrownExceptions)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(ex => SetWarningMessage(ex.ToTypeMessageString()));
		}

		private void SetSendText()
		{
			if (IsTransactionBuilder)
			{
				BuildTransactionButtonText = IsBusy ? BuildingTransactionButtonTextString : BuildTransactionButtonTextString;
				return;
			}

			BuildTransactionButtonText = IsHardwareBusy
				? WaitingForHardwareWalletButtonTextString
				: IsBusy
					? SendingTransactionButtonTextString
					: SendTransactionButtonTextString;
		}

		private void SetAmountWatermark(Money amount)
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
					Logger.LogTrace(ex);
				}

				AmountWatermarkText = amountUsd != 0
					? $"Amount (BTC) ~ ${amountUsd}"
					: "Amount (BTC)";
			}
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

			int feeTarget = -1; // 1 => 10 minutes
			if (IsSliderFeeUsed)
			{
				feeTarget = FeeTarget;
				if (allFeeEstimate != null)
				{
					int prevKey = allFeeEstimate.Estimations.Keys.First();
					foreach (int target in allFeeEstimate.Estimations.Keys)
					{
						if (feeTarget == target)
						{
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
			}
			else
			{
				FeeRate = null;
				if (allFeeEstimate != null)
				{
					// In decimal ',' means order of magnitude.
					// User could think it is decimal point but 3,5 means 35 Satoshi.
					// For this reason we treat ',' as an invalid character.
					if (!UserFeeText.Contains(",") && decimal.TryParse(UserFeeText, out decimal userFee))
					{
						FeeRate = new FeeRate(userFee);
						feeTarget = Constants.SevenDaysConfirmationTarget;
						foreach (var feeEstimate in allFeeEstimate.Estimations)
						{
							var target = feeEstimate.Key;
							var fee = feeEstimate.Value;
							if (FeeRate.SatoshiPerByte > fee)
							{
								feeTarget = target;
								break;
							}
						}
					}
				}
			}

			if (allFeeEstimate != null)
			{
				SetFees(allFeeEstimate, feeTarget);
				if (FeeRate is null)
				{
					FeeText = "";
					FeeToolTip = "";
				}
				else
				{
					switch (FeeDisplayFormat)
					{
						case FeeDisplayFormat.SatoshiPerByte:
							FeeText = $"(~ {FeeRate.SatoshiPerByte} sat/byte)";
							FeeToolTip = "Expected fee rate in satoshi / vbyte.";
							break;

						case FeeDisplayFormat.USD:
							FeeText = $"(~ ${UsdFee.ToString("0.##")})";
							FeeToolTip = $"Estimated total fees in USD. Exchange Rate: {(long)UsdExchangeRate} BTC/USD.";
							break;

						case FeeDisplayFormat.BTC:
							FeeText = $"(~ {EstimatedBtcFee.ToString(false, false)} BTC)";
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
			}

			SetAmountIfMax();
		}

		private void SetAmountIfMax()
		{
			if (IsMax)
			{
				AmountText = AllSelectedAmount == Money.Zero
					? "No Coins Selected"
					: $"~ {AllSelectedAmount.ToString(false, true)}";
			}
		}

		private void SetFees(AllFeeEstimate allFeeEstimate, int feeTarget)
		{
			if (IsSliderFeeUsed)
			{
				FeeRate = allFeeEstimate.GetFeeRate(feeTarget);
				UserFeeText = FeeRate.SatoshiPerByte.ToString();
			}

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
					if (Money.TryParse(AmountText.TrimStart('~', ' '), out Money amount))
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
					// Else whatever, do not change.
				}
			}

			if (FeeRate != null)
			{
				EstimatedBtcFee = FeeRate.GetTotalFee(vsize);
			}
			else
			{
				// This should not happen. Never.
				// If SatoshiPerByteFeeRate is null we will have problems when building the tx.
				EstimatedBtcFee = Money.Zero;
			}

			long all = selectedCoins.Sum(x => x.Amount);
			if (IsMax)
			{
				if (all != 0)
				{
					FeePercentage = 100 * (decimal)EstimatedBtcFee.Satoshi / all;
				}
			}
			else
			{
				if (Money.TryParse(AmountText.TrimStart('~', ' '), out Money amount) && amount.Satoshi != 0)
				{
					FeePercentage = 100 * (decimal)EstimatedBtcFee.Satoshi / amount.Satoshi;
				}
			}

			if (UsdExchangeRate != 0)
			{
				UsdFee = EstimatedBtcFee.ToUsd(UsdExchangeRate);
			}

			AllSelectedAmount = Math.Max(Money.Zero, all - EstimatedBtcFee);
		}

		private void SetFeeTargetLimits()
		{
			var allFeeEstimate = Global.Synchronizer?.AllFeeEstimate;

			if (allFeeEstimate != null)
			{
				MinimumFeeTarget = allFeeEstimate.Estimations.Min(x => x.Key); // This should be always 2, but bugs will be seen at least if it is not.
				MaximumFeeTarget = allFeeEstimate.Estimations.Max(x => x.Key);
			}
			else
			{
				MinimumFeeTarget = 2;
				MaximumFeeTarget = Constants.SevenDaysConfirmationTarget;
			}
		}

		private void TryResetInputsOnSuccess(string successMessage)
		{
			try
			{
				ResetUi();

				SetSuccessMessage(successMessage);
			}
			catch (Exception ex)
			{
				Logger.LogInfo(ex);
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
				Logger.LogWarning(ex);
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
				Logger.LogWarning(ex);
				var builder = new StringBuilder(ex.ToTypeMessageString());
				if (ex is AggregateException aggex)
				{
					foreach (var iex in aggex.InnerExceptions)
					{
						builder.Append(Environment.NewLine + iex.ToTypeMessageString());
					}
				}
				SetWarningMessage(builder.ToString());
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

		public string AmountText
		{
			get => _amountText;
			set => this.RaiseAndSetIfChanged(ref _amountText, value);
		}

		public string UserFeeText
		{
			get => _userFeeText;
			set => this.RaiseAndSetIfChanged(ref _userFeeText, value);
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

		public bool MinMaxFeeTargetsEqual => _minMaxFeeTargetsEqual?.Value ?? false;

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

		public Money EstimatedBtcFee
		{
			get => _estimatedBtcFee;
			set => this.RaiseAndSetIfChanged(ref _estimatedBtcFee, value);
		}

		public FeeRate FeeRate
		{
			get => _feeRate;
			set => this.RaiseAndSetIfChanged(ref _feeRate, value);
		}

		public decimal FeePercentage
		{
			get => _feePercentage;
			set => this.RaiseAndSetIfChanged(ref _feePercentage, value);
		}

		public decimal UsdExchangeRate => _usdExchangeRate?.Value ?? 0m;

		public Money AllSelectedAmount
		{
			get => _allSelectedAmount;
			set => this.RaiseAndSetIfChanged(ref _allSelectedAmount, value);
		}

		public ErrorDescriptors ValidatePassword() => PasswordHelper.ValidatePassword(Password);

		[ValidateMethod(nameof(ValidatePassword))]
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

			var labels = Global.WalletService.GetLabels();
			IEnumerable<string> suggestedWords = labels.Where(w => w.StartsWith(lastWord, StringComparison.InvariantCultureIgnoreCase))
				.Union(labels.Where(w => w.Contains(lastWord, StringComparison.InvariantCultureIgnoreCase)))
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

		public ErrorDescriptors ValidateAddress()
		{
			if (string.IsNullOrWhiteSpace(Address))
			{
				return ErrorDescriptors.Empty;
			}

			if (AddressStringParser.TryParseBitcoinAddress(Address, Global.Network, out _))
			{
				return ErrorDescriptors.Empty;
			}

			if (AddressStringParser.TryParseBitcoinUrl(Address, Global.Network, out _))
			{
				return ErrorDescriptors.Empty;
			}

			return new ErrorDescriptors(new ErrorDescriptor(ErrorSeverity.Error, "Invalid address."));
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

		public bool IsSliderFeeUsed
		{
			get => _isSliderFeeUsed;
			set => this.RaiseAndSetIfChanged(ref _isSliderFeeUsed, value);
		}

		public double FeeControlOpacity
		{
			get => _feeControlOpacity;
			set => this.RaiseAndSetIfChanged(ref _feeControlOpacity, value);
		}

		public bool IsCustomFee => _isCustomFee?.Value ?? false;

		public ReactiveCommand<Unit, Unit> BuildTransactionCommand { get; }

		public ReactiveCommand<Unit, Unit> MaxCommand { get; }

		public ReactiveCommand<Unit, Unit> FeeRateCommand { get; }

		public ReactiveCommand<BitcoinUrlBuilder, Unit> OnAddressPasteCommand { get; }

		public ReactiveCommand<KeyEventArgs, Unit> UserFeeTextKeyUpCommand { get; }

		public ReactiveCommand<PointerPressedEventArgs, Unit> FeeSliderClickedCommand { get; }

		public ReactiveCommand<bool, Unit> HighLightFeeSliderCommand { get; }

		public bool IsTransactionBuilder { get; }

		public override void OnOpen()
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

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

			_usdExchangeRate = Global.Synchronizer
				.WhenAnyValue(x => x.UsdExchangeRate)
				.ToProperty(this, x => x.UsdExchangeRate, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => SetFeesAndTexts());

			_isCustomFee = Global.UiConfig.WhenAnyValue(x => x.IsCustomFee)
				.ToProperty(this, x => x.IsCustomFee, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.IsCustomFee)
				.Where(x => !x)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => IsSliderFeeUsed = true);

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
