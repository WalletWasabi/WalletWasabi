using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.MathNet;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "wallet_action_send",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.DialogScreen)]
	public partial class SendViewModel : NavBarItemViewModel
	{
		private readonly Wallet _wallet;
		private readonly TransactionInfo _transactionInfo;

		[AutoNotify] private string _to;
		[AutoNotify] private decimal _amountBtc;
		[AutoNotify] private decimal _exchangeRate;
		[AutoNotify] private bool _isFixedAmount;
		[AutoNotify] private ObservableCollection<string> _priorLabels;
		[AutoNotify] private ObservableCollection<string> _labels;
		[AutoNotify] private bool _isPayJoin;
		[AutoNotify] private double[] _xAxisValues;
		[AutoNotify] private double[] _yAxisValues;
		[AutoNotify] private string[] _xAxisLabels;
		[AutoNotify] private double _xAxisCurrentValue = 36;
		[AutoNotify] private int _xAxisCurrentValueIndex;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private int _xAxisMinValue = 0;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private int _xAxisMaxValue = 9;
		[AutoNotify] private string? _payJoinEndPoint;

		private bool _parsingUrl;
		private bool _updatingCurrentValue;
		private double _lastXAxisCurrentValue;
		private FeeRate _feeRate;

		public SendViewModel(Wallet wallet) : base(NavigationMode.Normal)
		{
			_to = "";
			_wallet = wallet;
			_transactionInfo = new TransactionInfo();
			_labels = new ObservableCollection<string>();
			_lastXAxisCurrentValue = _xAxisCurrentValue;

			SelectionMode = NavBarItemSelectionMode.Button;

			ExchangeRate = _wallet.Synchronizer.UsdExchangeRate;
			PriorLabels = new();

			this.ValidateProperty(x => x.To, ValidateToField);
			this.ValidateProperty(x => x.AmountBtc, ValidateAmount);

			this.WhenAnyValue(x => x.To)
				.Subscribe(ParseToField);

			this.WhenAnyValue(x => x.AmountBtc)
				.Subscribe(x => _transactionInfo.Amount = new Money(x, MoneyUnit.BTC));

			this.WhenAnyValue(x => x.XAxisCurrentValue)
				.Subscribe(x =>
				{
					if (x > 0)
					{
						_feeRate = new FeeRate(GetYAxisValueFromXAxisCurrentValue(x));
						SetXAxisCurrentValueIndex(x);
					}
				});

			this.WhenAnyValue(x => x.XAxisCurrentValueIndex)
				.Subscribe(SetXAxisCurrentValue);

			this.WhenAnyValue(x => x.PayJoinEndPoint)
				.Subscribe(endPoint =>
				{
					if (endPoint is { })
					{
						_transactionInfo.PayJoinClient = GetPayjoinClient(endPoint);
						IsPayJoin = true;
					}
					else
					{
						IsPayJoin = false;
					}
				});

			Labels.ToObservableChangeSet().Subscribe(x => _transactionInfo.UserLabels = new SmartLabel(_labels.ToArray()));

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
			EnableBack = true;

			PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
			AutoPasteCommand = ReactiveCommand.CreateFromTask(async () => await OnAutoPasteAsync());

			var nextCommandCanExecute =
				this.WhenAnyValue(x => x.Labels, x => x.AmountBtc, x => x.To, x => x.XAxisCurrentValue).Select(_ => Unit.Default)
					.Merge(Observable.FromEventPattern(Labels, nameof(Labels.CollectionChanged)).Select(_ => Unit.Default))
					.Select(_ =>
					{
						var allFilled = !string.IsNullOrEmpty(To) && AmountBtc > 0 && Labels.Any();
						var hasError = Validations.Any;

						return allFilled && !hasError;
					});

			NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(), nextCommandCanExecute);

			EnableAutoBusyOn(NextCommand);
		}

		public ICommand PasteCommand { get; }

		public ICommand AutoPasteCommand { get; }

		private async Task OnAutoPasteAsync()
		{
			var isAutoPasteEnabled = Services.UiConfig.Autocopy;

			if (string.IsNullOrEmpty(To) && isAutoPasteEnabled)
			{
				await OnPasteAsync(pasteIfInvalid: false);
			}
		}

		private async Task OnPasteAsync(bool pasteIfInvalid = true)
		{
			var text = await Application.Current.Clipboard.GetTextAsync();

			_parsingUrl = true;

			if (!TryParseUrl(text) && pasteIfInvalid)
			{
				To = text;
			}

			_parsingUrl = false;
		}

		private async Task OnNextAsync()
		{
			var transactionInfo = _transactionInfo;
			var targetAnonymitySet = _wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();
			var mixedCoins = _wallet.Coins.Where(x => x.HdPubKey.AnonymitySet >= targetAnonymitySet).ToList();
			var totalMixedCoinsAmount = Money.FromUnit(mixedCoins.Sum(coin => coin.Amount), MoneyUnit.Satoshi);
			transactionInfo.Coins = mixedCoins;
			transactionInfo.FeeRate = _feeRate;

			if (transactionInfo.Amount > totalMixedCoinsAmount)
			{
				Navigate().To(new PrivacyControlViewModel(_wallet, transactionInfo));
				return;
			}

			try
			{
				if (IsPayJoin)
				{
					await BuildTransactionAsPayJoinAsync(transactionInfo);
				}
				else
				{
					await BuildTransactionAsNormalAsync(transactionInfo, totalMixedCoinsAmount);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync("Transaction Building", ex.ToUserFriendlyString(), "Wasabi was unable to create your transaction.");
			}
		}

		private async Task BuildTransactionAsNormalAsync(TransactionInfo transactionInfo, Money totalMixedCoinsAmount)
		{
			try
			{
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new OptimisePrivacyViewModel(_wallet, transactionInfo, txRes));
			}
			catch (InsufficientBalanceException)
			{
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo.Address, totalMixedCoinsAmount, transactionInfo.Labels, transactionInfo.FeeRate, transactionInfo.Coins, subtractFee: true));
				var dialog = new InsufficientBalanceDialogViewModel(BalanceType.Private, txRes, _wallet.Synchronizer.UsdExchangeRate);
				var result = await NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);

				if (result.Result)
				{
					Navigate().To(new OptimisePrivacyViewModel(_wallet, transactionInfo, txRes));
				}
				else
				{
					Navigate().To(new PrivacyControlViewModel(_wallet, transactionInfo));
				}
			}
		}

		private async Task BuildTransactionAsPayJoinAsync(TransactionInfo transactionInfo)
		{
			try
			{
				// Do not add the PayJoin client yet, it will be added before broadcasting.
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new TransactionPreviewViewModel(_wallet, transactionInfo, txRes));
			}
			catch (InsufficientBalanceException)
			{
				await ShowErrorAsync("Transaction Building", "There are not enough private funds to cover the transaction fee", "Wasabi was unable to create your transaction.");
				Navigate().To(new PrivacyControlViewModel(_wallet, transactionInfo));
			}
		}

		private IPayjoinClient? GetPayjoinClient(string endPoint)
		{
			if (!string.IsNullOrWhiteSpace(endPoint) &&
				Uri.IsWellFormedUriString(endPoint, UriKind.Absolute))
			{
				var payjoinEndPointUri = new Uri(endPoint);
				if (!Services.Config.UseTor)
				{
					if (payjoinEndPointUri.DnsSafeHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
					{
						Logger.LogWarning("PayJoin server is an onion service but Tor is disabled. Ignoring...");
						return null;
					}

					if (Services.Config.Network == Network.Main && payjoinEndPointUri.Scheme != Uri.UriSchemeHttps)
					{
						Logger.LogWarning("PayJoin server is not exposed as an onion service nor https. Ignoring...");
						return null;
					}
				}

				IHttpClient httpClient = Services.ExternalHttpClientFactory.NewHttpClient(() => payjoinEndPointUri, Mode.DefaultCircuit);
				return new PayjoinClient(payjoinEndPointUri, httpClient);
			}

			return null;
		}

		private TimeSpan CalculateConfirmationTime(double targetBlock)
		{
			var timeInMinutes = Math.Ceiling(targetBlock) * 10;
			var time = TimeSpan.FromMinutes(timeInMinutes);
			return time;
		}

		private void SetXAxisCurrentValueIndex(double xAxisCurrentValue)
		{
			if (!_updatingCurrentValue)
			{
				_updatingCurrentValue = true;
				if (_xAxisValues is not null)
				{
					XAxisCurrentValueIndex = GetCurrentValueIndex(xAxisCurrentValue, _xAxisValues);
				}
				_updatingCurrentValue = false;
			}
		}

		private void SetXAxisCurrentValue(int xAxisCurrentValueIndex)
		{
			if (_xAxisValues is not null)
			{
				if (!_updatingCurrentValue)
				{
					_updatingCurrentValue = true;
					var index = _xAxisValues.Length - xAxisCurrentValueIndex - 1;
					XAxisCurrentValue = _xAxisValues[index];
					_updatingCurrentValue = false;
				}
			}
		}

		private void ValidateAmount(IValidationErrors errors)
		{
			if (AmountBtc > Constants.MaximumNumberOfBitcoins)
			{
				errors.Add(ErrorSeverity.Error, "Amount must be less than the total supply of BTC.");
			}
			else if (AmountBtc > _wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC))
			{
				errors.Add(ErrorSeverity.Error, "Insufficient funds to cover the amount requested.");
			}
			else if (AmountBtc <= 0)
			{
				errors.Add(ErrorSeverity.Error, "Amount must be more than 0 BTC");
			}
		}

		private void ValidateToField(IValidationErrors errors)
		{
			if (!string.IsNullOrWhiteSpace(To) &&
				!AddressStringParser.TryParse(To, _wallet.Network, out _))
			{
				errors.Add(ErrorSeverity.Error, "Input a valid BTC address or URL.");
			}
			else if (IsPayJoin && _wallet.KeyManager.IsHardwareWallet)
			{
				errors.Add(ErrorSeverity.Error, "PayJoin is not possible with hardware wallets.");
			}
		}

		private void ParseToField(string s)
		{
			if (_parsingUrl)
			{
				return;
			}

			_parsingUrl = true;

			Dispatcher.UIThread.Post(() =>
			{
				TryParseUrl(s);

				_parsingUrl = false;
			});
		}

		private bool TryParseUrl(string text)
		{
			bool result = false;

			if (AddressStringParser.TryParse(text, _wallet.Network, out BitcoinUrlBuilder? url))
			{
				result = true;
				SmartLabel label = url.Label;

				if (!label.IsEmpty)
				{
					Labels.Clear();

					foreach (var labelString in label.Labels)
					{
						Labels.Add(labelString);
					}
				}

				if (url.UnknowParameters.TryGetValue("pj", out var endPoint))
				{
					PayJoinEndPoint = endPoint;
				}
				else
				{
					PayJoinEndPoint = null;
				}

				if (url.Address is { })
				{
					_transactionInfo.Address = url.Address;
					To = url.Address.ToString();
				}

				if (url.Amount is { })
				{
					AmountBtc = url.Amount.ToDecimal(MoneyUnit.BTC);
					IsFixedAmount = true;
				}
				else
				{
					IsFixedAmount = false;
				}
			}
			else
			{
				IsFixedAmount = false;
				PayJoinEndPoint = null;
			}

			return result;
		}

		protected override void OnNavigatedFrom(bool isInHistory)
		{
			base.OnNavigatedFrom(isInHistory);
			_lastXAxisCurrentValue = XAxisCurrentValue;
			_transactionInfo.ConfirmationTimeSpan = CalculateConfirmationTime(_lastXAxisCurrentValue);
		}

		protected override void OnNavigatedTo(bool inHistory, CompositeDisposable disposables)
		{
			if (!inHistory)
			{
				To = "";
				AmountBtc = 0;
				Labels.Clear();
				ClearValidations();
			}
			else
			{
				XAxisCurrentValue = _lastXAxisCurrentValue;
			}

			_wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => ExchangeRate = x)
				.DisposeWith(disposables);

			_wallet.TransactionProcessor.WhenAnyValue(x => x.Coins)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					PriorLabels.AddRange(x.SelectMany(coin => coin.HdPubKey.Label.Labels));

					PriorLabels = new ObservableCollection<string>(PriorLabels.Distinct());
				})
				.DisposeWith(disposables);

			PriorLabels.AddRange(_wallet
				.KeyManager
				.GetLabels()
				.Select(x => x.ToString()
					.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
					.SelectMany(x => x));

			PriorLabels = new ObservableCollection<string>(PriorLabels.Distinct());

			var feeProvider = _wallet.FeeProvider;
			Observable
				.FromEventPattern(feeProvider, nameof(feeProvider.AllFeeEstimateChanged))
				.Select(x => (x.EventArgs as AllFeeEstimate)!.Estimations)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(UpdateFeeEstimates)
				.DisposeWith(disposables);

			if (feeProvider.AllFeeEstimate is { })
			{
				UpdateFeeEstimates(feeProvider.AllFeeEstimate.Estimations);
			}

			base.OnNavigatedTo(inHistory, disposables);
		}

		private static readonly double[] TestNetXAxisValues =
		{
			1,
			2,
			3,
			6,
			18,
			36,
			72,
			144,
			432,
			1008
		};

		private static readonly double[] TestNetYAxisValues =
		{
			185,
			123,
			123,
			102,
			97,
			57,
			22,
			7,
			4,
			4
		};

		private void UpdateFeeEstimates(Dictionary<int, int> feeEstimates)
		{
			string[] xAxisLabels;
			double[] xAxisValues;
			double[] yAxisValues;

			if (_wallet.Network != Network.TestNet)
			{
				var labels = feeEstimates.Select(x => x.Key)
					.Select(x => FeeTargetTimeConverter.Convert(x, "m", "h", "h", "d", "d"))
					.Reverse()
					.ToArray();

				var xs = feeEstimates.Select(x => (double)x.Key).ToArray();
				var ys = feeEstimates.Select(x => (double)x.Value).ToArray();
#if true
				GetSmoothValuesSubdivide(xs, ys, out var ts, out var xts);
				xAxisValues = ts.ToArray();
				yAxisValues = xts.ToArray();
#else
				xAxisValues = xs.Reverse().ToArray();
				yAxisValues = ys.Reverse().ToArray();
#endif
				xAxisLabels = labels;
			}
			else
			{
#if true
				GetSmoothValuesSubdivide(TestNetXAxisValues, TestNetYAxisValues, out var ts, out var xts);
				xAxisValues = ts.ToArray();
				yAxisValues = xts.ToArray();
#else
				xAxisValues = xs.Reverse().ToArray();
				yAxisValues = ys.Reverse().ToArray();
#endif
				var labels = TestNetXAxisValues.Select(x => x)
					.Select(x => FeeTargetTimeConverter.Convert((int)x, "m", "h", "h", "d", "d"))
					.Reverse()
					.ToArray();
				xAxisLabels = labels;
			}

			_updatingCurrentValue = true;
			XAxisLabels = xAxisLabels;
			XAxisValues = xAxisValues;
			YAxisValues = yAxisValues;
			XAxisMinValue = 0;
			XAxisMaxValue = xAxisValues.Length - 1;
			XAxisCurrentValue = Math.Clamp(XAxisCurrentValue, XAxisMinValue, XAxisMaxValue);
			XAxisCurrentValueIndex = GetCurrentValueIndex(XAxisCurrentValue, XAxisValues);
			_updatingCurrentValue = false;
		}

		private int GetCurrentValueIndex(double xAxisCurrentValue, double[] xAxisValues)
		{
			for (var i = 0; i < xAxisValues.Length; i++)
			{
				if (xAxisValues[i] <= xAxisCurrentValue)
				{
					var index = xAxisValues.Length - i - 1;
					return index;
				}
			}

			return 0;
		}

		private void GetSmoothValuesSubdivide(double[] xs, double[] ys, out List<double> ts, out List<double> xts)
		{
			const int Divisions = 256;

			ts = new List<double>();
			xts = new List<double>();

			if (xs.Length > 2)
			{
				var spline = CubicSpline.InterpolatePchipSorted(xs, ys);

				for (var i = 0; i < xs.Length - 1; i++)
				{
					var a = xs[i];
					var b = xs[i + 1];
					var range = b - a;
					var step = range / Divisions;

					var t0 = xs[i];
					ts.Add(t0);
					var xt0 = spline.Interpolate(xs[i]);
					xts.Add(xt0);

					for (var t = a + step; t < b; t += step)
					{
						var xt = spline.Interpolate(t);
						ts.Add(t);
						xts.Add(xt);
					}
				}

				var tn = xs[^1];
				ts.Add(tn);
				var xtn = spline.Interpolate(xs[^1]);
				xts.Add(xtn);
			}
			else
			{
				for (var i = 0; i < xs.Length; i++)
				{
					ts.Add(xs[i]);
					xts.Add(ys[i]);
				}
			}

			ts.Reverse();
			xts.Reverse();
		}

		private decimal GetYAxisValueFromXAxisCurrentValue(double xValue)
		{
			if (_xAxisValues is { } && _yAxisValues is { })
			{
				var x = _xAxisValues.Reverse().ToArray();
				var y = _yAxisValues.Reverse().ToArray();
				double t = xValue;
				var spline = CubicSpline.InterpolatePchipSorted(x, y);
				var interpolated = (decimal)spline.Interpolate(t);
				return Math.Clamp(interpolated, (decimal)y[^1], (decimal)y[0]);
			}

			return XAxisMaxValue;
		}
	}
}
