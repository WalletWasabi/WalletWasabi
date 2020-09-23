using Avalonia.Input;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Exceptions;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.Suggestions;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public abstract class SendControlViewModel : WasabiDocumentTabViewModel, IWalletViewModel
	{
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
		private string _customChangeAddress;
		private string _labelToolTip;
		private string _feeToolTip;
		private string _amountWaterMarkText;
		private bool _isBusy;
		private bool _isHardwareBusy;
		private bool _isCustomFee;

		private const string WaitingForHardwareWalletButtonTextString = "Waiting for Hardware Wallet...";

		private FeeDisplayFormat _feeDisplayFormat;
		private bool _isSliderFeeUsed = true;
		private double _feeControlOpacity;

		protected SendControlViewModel(Wallet wallet, string title)
			: base(title)
		{
			Global = Locator.Current.GetService<Global>();
			Wallet = wallet;

			LabelSuggestion = new SuggestLabelViewModel();
			BuildTransactionButtonText = DoButtonText;

			this.ValidateProperty(x => x.Address, ValidateAddress);
			this.ValidateProperty(x => x.CustomChangeAddress, ValidateCustomChangeAddress);
			this.ValidateProperty(x => x.Password, ValidatePassword);
			this.ValidateProperty(x => x.UserFeeText, ValidateUserFeeText);

			ResetUi();
			SetAmountWatermark(Money.Zero);

			CoinList = new CoinListViewModel(Wallet, displayCommonOwnershipWarning: true);

			Observable.FromEventPattern(CoinList, nameof(CoinList.SelectionChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => SetFeesAndTexts());

			_minMaxFeeTargetsEqual = this.WhenAnyValue(x => x.MinimumFeeTarget, x => x.MaximumFeeTarget, (x, y) => x == y)
				.ToProperty(this, x => x.MinMaxFeeTargetsEqual, scheduler: RxApp.MainThreadScheduler);

			SetFeeTargetLimits();
			FeeTarget = Global.UiConfig.FeeTarget;
			FeeDisplayFormat = (FeeDisplayFormat)(Enum.ToObject(typeof(FeeDisplayFormat), Global.UiConfig.FeeDisplayFormat) ?? FeeDisplayFormat.SatoshiPerByte);
			SetFeesAndTexts();

			this.WhenAnyValue(x => x.AmountText)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (Money.TryParse(x.TrimStart('~', ' '), out Money amountBtc))
					{
						SetAmountWatermark(amountBtc);
					}
					else
					{
						SetAmountWatermark(Money.Zero);
					}

					SetFees();
				});

			AmountKeyUpCommand = ReactiveCommand.Create((KeyEventArgs key) =>
			{
				var amount = AmountText;
				if (IsMax)
				{
					SetFeesAndTexts();
				}
				else
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
			});

			this.WhenAnyValue(x => x.IsBusy, x => x.IsHardwareBusy)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => BuildTransactionButtonText = IsHardwareBusy
						? WaitingForHardwareWalletButtonTextString
						: IsBusy
							? DoingButtonText
							: DoButtonText);

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

			MaxCommand = ReactiveCommand.Create(() => IsMax = !IsMax, outputScheduler: RxApp.MainThreadScheduler);

			this.WhenAnyValue(x => x.IsMax)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					if (IsMax)
					{
						SetFeesAndTexts();

						LabelToolTip = "Spending whole coins does not generate change, thus labeling is unnecessary.";
					}
					else
					{
						AmountText = "0.0";

						LabelToolTip = "Who can link this transaction to you? E.g.: \"Max, BitPay\"";
					}
				});

			// Triggering the detection of same address values.
			this.WhenAnyValue(x => x.Address)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(CustomChangeAddress)));

			this.WhenAnyValue(x => x.CustomChangeAddress)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(Address)));

			this.WhenAnyValue(x => x.IsCustomChangeAddressVisible)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(Address));
					this.RaisePropertyChanged(nameof(CustomChangeAddress));
				});

			FeeRateCommand = ReactiveCommand.Create(ChangeFeeRateDisplay, outputScheduler: RxApp.MainThreadScheduler);

			OnAddressPasteCommand = ReactiveCommand.Create((BitcoinUrlBuilder url) => OnAddressPaste(url));

			BuildTransactionCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					IsBusy = true;
					MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.BuildingTransaction);

					var label = new SmartLabel(LabelSuggestion.Label);
					LabelSuggestion.Label = label;
					if (!IsMax && label.IsEmpty)
					{
						NotificationHelpers.Warning("Label is required.", "");
						return;
					}

					var selectedCoinViewModels = CoinList.Coins.Where(cvm => cvm.IsSelected);
					var selectedCoinReferences = selectedCoinViewModels.Select(cvm => cvm.Model.OutPoint).ToList();

					if (!selectedCoinReferences.Any())
					{
						NotificationHelpers.Warning("No coins are selected to spend.", "");
						return;
					}

					BitcoinAddress address;
					try
					{
						address = BitcoinAddress.Create(Address, Global.Network);
					}
					catch (FormatException)
					{
						NotificationHelpers.Warning("Invalid address.", "");
						return;
					}

					var requests = new List<DestinationRequest>();

					if (IsCustomChangeAddressVisible && !string.IsNullOrWhiteSpace(CustomChangeAddress))
					{
						try
						{
							var customChangeAddress = BitcoinAddress.Create(CustomChangeAddress, Global.Network);

							if (customChangeAddress == address)
							{
								NotificationHelpers.Warning("The active address and the change address cannot be the same.", "");
								return;
							}

							requests.Add(new DestinationRequest(customChangeAddress, MoneyRequest.CreateChange(subtractFee: true), label));
						}
						catch (FormatException)
						{
							NotificationHelpers.Warning("Invalid custom change address.", "");
							return;
						}
					}

					MoneyRequest moneyRequest;
					if (IsMax)
					{
						moneyRequest = MoneyRequest.CreateAllRemaining(subtractFee: true);
					}
					else
					{
						if (!Money.TryParse(AmountText, out Money amount) || amount == Money.Zero)
						{
							NotificationHelpers.Warning("Invalid amount.");
							return;
						}

						if (amount == selectedCoinViewModels.Sum(x => x.Amount))
						{
							NotificationHelpers.Warning("Looks like you want to spend whole coins. Try Max button instead.", "");
							return;
						}
						moneyRequest = MoneyRequest.Create(amount, subtractFee: false);
					}

					if (FeeRate is null || FeeRate.SatoshiPerByte < 1)
					{
						NotificationHelpers.Warning("Invalid fee rate.", "");
						return;
					}

					var feeStrategy = FeeStrategy.CreateFromFeeRate(FeeRate);

					var activeDestinationRequest = new DestinationRequest(address, moneyRequest, label);
					requests.Add(activeDestinationRequest);

					var intent = new PaymentIntent(requests);
					try
					{
						MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.DequeuingSelectedCoins);
						OutPoint[] toDequeue = selectedCoinViewModels.Where(x => x.CoinJoinInProgress).Select(x => x.Model.OutPoint).ToArray();
						if (toDequeue != null && toDequeue.Any())
						{
							await Wallet.ChaumianClient.DequeueCoinsFromMixAsync(toDequeue, DequeueReason.TransactionBuilding);
						}
					}
					catch
					{
						NotificationHelpers.Error("Cannot spend mixing coins.", "");
						return;
					}
					finally
					{
						MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusType.DequeuingSelectedCoins);
					}

					if (!Wallet.KeyManager.IsWatchOnly)
					{
						try
						{
							PasswordHelper.GetMasterExtKey(Wallet.KeyManager, Password, out string compatiblityPasswordUsed); // We could use TryPassword but we need the exception.
							if (compatiblityPasswordUsed != null)
							{
								Password = compatiblityPasswordUsed; // Overwrite the password for BuildTransaction function.
								NotificationHelpers.Warning(PasswordHelper.CompatibilityPasswordWarnMessage);
							}
						}
						catch (SecurityException ex)
						{
							NotificationHelpers.Error(ex.Message, "");
							return;
						}
						catch (Exception ex)
						{
							Logger.LogError(ex);
							NotificationHelpers.Error(ex.ToUserFriendlyString());
							return;
						}
					}

					await BuildTransaction(Password, intent, feeStrategy, allowUnconfirmed: true, allowedInputs: selectedCoinReferences);
				}
				catch (InsufficientBalanceException ex)
				{
					Money needed = ex.Minimum - ex.Actual;
					NotificationHelpers.Error($"Not enough coins selected. You need an estimated {needed.ToString(false, true)} BTC more to make this transaction.", "");
				}
				catch (HttpRequestException ex)
				{
					NotificationHelpers.Error(ex.ToUserFriendlyString());
					Logger.LogError(ex);
				}
				catch (Exception ex)
				{
					NotificationHelpers.Error(ex.ToUserFriendlyString(), sender: Wallet);
					Logger.LogError(ex);
				}
				finally
				{
					MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusType.BuildingTransaction, StatusType.SigningTransaction, StatusType.BroadcastingTransaction);
					IsBusy = false;
				}
			},
			this.WhenAny(x => x.IsMax, x => x.AmountText, x => x.Address, x => x.IsBusy,
				(isMax, amount, address, busy) => (isMax.Value || !string.IsNullOrWhiteSpace(amount.Value)) && !string.IsNullOrWhiteSpace(Address) && !IsBusy)
				.ObserveOn(RxApp.MainThreadScheduler));

			UserFeeTextKeyUpCommand = ReactiveCommand.Create((KeyEventArgs key) =>
			{
				IsSliderFeeUsed = !IsCustomFee;
				SetFeesAndTexts();
			});

			FeeSliderClickedCommand = ReactiveCommand.Create((PointerPressedEventArgs mouse) => IsSliderFeeUsed = true);

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
				.Merge(AmountKeyUpCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					NotificationHelpers.Error(ex.ToUserFriendlyString());
					Logger.LogError(ex);
				});
		}

		protected Global Global { get; }

		protected Wallet Wallet { get; }

		Wallet IWalletViewModel.Wallet => Wallet;

		private FeeDisplayFormat FeeDisplayFormat
		{
			get => _feeDisplayFormat;
			set
			{
				_feeDisplayFormat = value;
				Global.UiConfig.FeeDisplayFormat = (int)value;
			}
		}

		public abstract string DoButtonText { get; }
		public abstract string DoingButtonText { get; }

		public SuggestLabelViewModel LabelSuggestion { get; }

		public CoinListViewModel CoinList { get; }

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

		public bool IsWatchOnly => Wallet.KeyManager.IsWatchOnly;

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public string Address
		{
			get => _address;
			set => this.RaiseAndSetIfChanged(ref _address, value?.Trim());
		}

		public string CustomChangeAddress
		{
			get => _customChangeAddress;
			set => this.RaiseAndSetIfChanged(ref _customChangeAddress, value?.Trim());
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

		public bool IsCustomFee
		{
			get => _isCustomFee;
			private set => this.RaiseAndSetIfChanged(ref _isCustomFee, value);
		}

		public bool IsCustomChangeAddressVisible => Global.UiConfig.IsCustomChangeAddress && !IsMax;

		public ReactiveCommand<Unit, Unit> BuildTransactionCommand { get; }

		public ReactiveCommand<Unit, bool> MaxCommand { get; }

		public ReactiveCommand<Unit, Unit> FeeRateCommand { get; }

		public ReactiveCommand<BitcoinUrlBuilder, Unit> OnAddressPasteCommand { get; }

		public ReactiveCommand<KeyEventArgs, Unit> UserFeeTextKeyUpCommand { get; }

		public ReactiveCommand<PointerPressedEventArgs, bool> FeeSliderClickedCommand { get; }

		public ReactiveCommand<bool, Unit> HighLightFeeSliderCommand { get; }

		public ReactiveCommand<KeyEventArgs, Unit> AmountKeyUpCommand { get; }

		protected virtual void ResetUi()
		{
			LabelSuggestion.Reset();
			Address = "";
			CustomChangeAddress = "";
			Password = "";
			AllSelectedAmount = Money.Zero;
			IsMax = false;
			LabelToolTip = "Who can link this transaction to you? E.g.: \"Max, BitPay\"";
			AmountText = "0.0";
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
			SetFees();

			SetAmountIfMax();
		}

		private void SetFees()
		{
			AllFeeEstimate allFeeEstimate = Global.FeeProviders?.AllFeeEstimate;

			if (allFeeEstimate is { })
			{
				int feeTarget = -1; // 1 => 10 minutes
				if (IsSliderFeeUsed)
				{
					feeTarget = FeeTarget;

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
				else
				{
					FeeRate = null;

					// In decimal ',' means order of magnitude.
					// User could think it is decimal point but 3,5 means 35 Satoshi.
					// For this reason we treat ',' as an invalid character.
					if (TryParseUserFee(out decimal userFee))
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
					// If FeeRate is null we will have problems when building the tx.
					EstimatedBtcFee = Money.Zero;
				}

				long all = selectedCoins.Sum(x => x.Amount);
				long theAmount = (IsMax, Money.TryParse(AmountText.TrimStart('~', ' '), out Money value)) switch
				{
					(true, _) => all,
					(false, true) => value.Satoshi,
					(false, false) => 0
				};

				FeePercentage = theAmount != 0
					? 100 * (decimal)EstimatedBtcFee.Satoshi / theAmount
					: 0;

				if (UsdExchangeRate != 0)
				{
					UsdFee = EstimatedBtcFee.ToUsd(UsdExchangeRate);
				}

				AllSelectedAmount = Math.Max(Money.Zero, all - EstimatedBtcFee);
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
							FeeText = $"(~ {FeeRate.SatoshiPerByte} sat/vByte)";
							FeeToolTip = "Expected fee rate in satoshi/vByte.";
							break;

						case FeeDisplayFormat.USD:
							FeeText = $"(~ ${UsdFee:0.##})";
							FeeToolTip = $"Estimated total fees in USD. Exchange Rate: {(long)UsdExchangeRate} USD/BTC.";
							break;

						case FeeDisplayFormat.BTC:
							FeeText = $"(~ {EstimatedBtcFee.ToString(false, false)} BTC)";
							FeeToolTip = "Estimated total fees in BTC.";
							break;

						case FeeDisplayFormat.Percentage:
							FeeText = $"(~ {FeePercentage:0.#} %)";
							FeeToolTip = "Expected percentage of fees against the amount to be sent.";
							break;

						default:
							throw new NotSupportedException("This is impossible.");
					}
				}
			}
		}

		private void SetAmountIfMax()
		{
			if (IsMax)
			{
				AmountText = AllSelectedAmount == Money.Zero
					? FeePercentage >= 100
						? "Too high fee"
						: "No Coins Selected"
					: $"~ {AllSelectedAmount.ToString(false, true)}";
			}
		}

		private void SetFeeTargetLimits()
		{
			var allFeeEstimate = Global.FeeProviders?.AllFeeEstimate;

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

		private bool TryParseUserFee(out decimal userFee)
		{
			userFee = default;
			var userFeeText = UserFeeText;
			return
				userFeeText is { }
				&& !userFeeText.Contains(",")
				&& decimal.TryParse(userFeeText, out userFee)
				&& (userFee * 1_000) < Constants.MaxSatoshisSupply
				&& userFee > 0;
		}

		private void ValidateUserFeeText(IValidationErrors errors)
		{
			if (!TryParseUserFee(out _))
			{
				errors.Add(ErrorSeverity.Error, "Invalid fee.");
			}
		}

		private void ValidatePassword(IValidationErrors errors) => PasswordHelper.ValidatePassword(errors, Password);

		private void ValidateAddress(IValidationErrors errors)
		{
			if (string.IsNullOrWhiteSpace(Address))
			{
				return;
			}

			if (Address == CustomChangeAddress && IsCustomChangeAddressVisible)
			{
				errors.Add(ErrorSeverity.Error, "The active address and the change address cannot be the same.");
			}

			if (AddressStringParser.TryParseBitcoinAddress(Address, Global.Network, out _))
			{
				return;
			}

			if (AddressStringParser.TryParseBitcoinUrl(Address, Global.Network, out _))
			{
				return;
			}

			errors.Add(ErrorSeverity.Error, "Invalid address.");
		}

		private void ValidateCustomChangeAddress(IValidationErrors errors)
		{
			if (string.IsNullOrWhiteSpace(CustomChangeAddress))
			{
				return;
			}

			if (IsMax)
			{
				return;
			}

			if (Address == CustomChangeAddress && IsCustomChangeAddressVisible)
			{
				errors.Add(ErrorSeverity.Error, "The active address and the change address cannot be the same.");
			}

			if (AddressStringParser.TryParseBitcoinAddress(CustomChangeAddress, Global.Network, out _))
			{
				return;
			}

			errors.Add(ErrorSeverity.Error, "Invalid change address.");
		}

		public override void OnOpen(CompositeDisposable disposables)
		{
			Observable
				.FromEventPattern<AllFeeEstimate>(Global.FeeProviders, nameof(Global.FeeProviders.AllFeeEstimateChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
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
				})
				.DisposeWith(disposables);

			_usdExchangeRate = Global.Synchronizer
				.WhenAnyValue(x => x.UsdExchangeRate)
				.ToProperty(this, x => x.UsdExchangeRate, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(disposables);

			this.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => SetFeesAndTexts());

			Global.UiConfig.WhenAnyValue(x => x.IsCustomFee)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => IsCustomFee = x)
				.DisposeWith(disposables);

			this.WhenAnyValue(x => x.IsCustomFee)
				.Where(x => !x)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => IsSliderFeeUsed = true);

			Observable
				.Merge(Global.UiConfig.WhenAnyValue(x => x.IsCustomChangeAddress))
				.Merge(this.WhenAnyValue(x => x.IsMax))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(IsCustomChangeAddressVisible)))
				.DisposeWith(disposables);

			base.OnOpen(disposables);
		}

		protected abstract Task BuildTransaction(string password, PaymentIntent payments, FeeStrategy feeStrategy, bool allowUnconfirmed = false, IEnumerable<OutPoint> allowedInputs = null);

		protected virtual void OnAddressPaste(BitcoinUrlBuilder url)
		{
			SmartLabel label = url.Label;
			if (!label.IsEmpty)
			{
				LabelSuggestion.Label = label;
			}

			if (url.Amount != null)
			{
				AmountText = url.Amount.ToString(false, true);
			}
		}

		public override bool OnClose()
		{
			CoinList.OnClose();

			return base.OnClose();
		}
	}
}
