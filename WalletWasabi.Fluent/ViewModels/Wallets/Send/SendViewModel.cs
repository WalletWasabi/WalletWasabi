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
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.MathNet;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;
using WalletWasabi.WebClients.Wasabi;
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
		private readonly WalletViewModel _owner;
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

		public SendViewModel(WalletViewModel walletVm, TransactionBroadcaster broadcaster, Config config, UiConfig uiConfig, HttpClientFactory externalHttpClientFactory) : base(NavigationMode.Normal)
		{
			_to = "";
			_owner = walletVm;
			_transactionInfo = new TransactionInfo();
			_labels = new ObservableCollection<string>();
			_lastXAxisCurrentValue = _xAxisCurrentValue;

			SelectionMode = NavBarItemSelectionMode.Button;

			ExchangeRate = walletVm.Wallet.Synchronizer.UsdExchangeRate;
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
						_transactionInfo.PayJoinClient = GetPayjoinClient(endPoint, config, externalHttpClientFactory);
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

			PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPaste());
			AutoPasteCommand = ReactiveCommand.CreateFromTask(async () => await OnAutoPaste(uiConfig));

			var nextCommandCanExecute =
				this.WhenAnyValue(x => x.Labels, x => x.AmountBtc, x => x.To, x => x.XAxisCurrentValue).Select(_ => Unit.Default)
					.Merge(Observable.FromEventPattern(Labels, nameof(Labels.CollectionChanged)).Select(_ => Unit.Default))
					.Select(_ =>
					{
						var allFilled = !string.IsNullOrEmpty(To) && AmountBtc > 0 && Labels.Any();
						var hasError = Validations.Any;

						return allFilled && !hasError;
					});

			NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNext(broadcaster), nextCommandCanExecute);

			EnableAutoBusyOn(NextCommand);
		}

		public ICommand PasteCommand { get; }

		public ICommand AutoPasteCommand { get; }

		private async Task OnAutoPaste(UiConfig uiConfig)
		{
			var isAutoPasteEnabled = uiConfig.Autocopy;

			if (string.IsNullOrEmpty(To) && isAutoPasteEnabled)
			{
				await OnPaste(pasteIfInvalid: false);
			}
		}

		private async Task OnPaste(bool pasteIfInvalid = true)
		{
			var text = await Application.Current.Clipboard.GetTextAsync();

			_parsingUrl = true;

			if (!TryParseUrl(text) && pasteIfInvalid)
			{
				To = text;
			}

			_parsingUrl = false;
		}

		private async Task OnNext(TransactionBroadcaster broadcaster)
		{
			var transactionInfo = _transactionInfo;
			var wallet = _owner.Wallet;
			var targetAnonymitySet = wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();
			var mixedCoins = wallet.Coins.Where(x => x.HdPubKey.AnonymitySet >= targetAnonymitySet).ToList();
			var totalMixedCoinsAmount = Money.FromUnit(mixedCoins.Sum(coin => coin.Amount), MoneyUnit.Satoshi);
			transactionInfo.Coins = mixedCoins;
			transactionInfo.FeeRate = _feeRate;

			if (transactionInfo.Amount > totalMixedCoinsAmount)
			{
				Navigate().To(new PrivacyControlViewModel(wallet, transactionInfo, broadcaster));
				return;
			}

			try
			{
				if (IsPayJoin)
				{
					await BuildTransactionAsPayJoinAsync(wallet, transactionInfo, broadcaster);
				}
				else
				{
					await BuildTransactionAsNormalAsync(wallet, transactionInfo, broadcaster, totalMixedCoinsAmount);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				await ShowErrorAsync("Transaction Building", ex.ToUserFriendlyString(), "Wasabi was unable to create your transaction.");
			}
		}

		private async Task BuildTransactionAsNormalAsync(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster, Money totalMixedCoinsAmount)
		{
			try
			{
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(wallet, transactionInfo));
				Navigate().To(new OptimisePrivacyViewModel(wallet, transactionInfo, broadcaster, txRes));
			}
			catch (InsufficientBalanceException)
			{
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(wallet, transactionInfo.Address, totalMixedCoinsAmount, transactionInfo.Labels, transactionInfo.FeeRate, transactionInfo.Coins, subtractFee: true));
				var dialog = new InsufficientBalanceDialogViewModel(BalanceType.Private, txRes, wallet.Synchronizer.UsdExchangeRate);
				var result = await NavigateDialog(dialog, NavigationTarget.DialogScreen);

				if (result.Result)
				{
					Navigate().To(new OptimisePrivacyViewModel(wallet, transactionInfo, broadcaster, txRes));
				}
				else
				{
					Navigate().To(new PrivacyControlViewModel(wallet, transactionInfo, broadcaster));
				}
			}
		}

		private async Task BuildTransactionAsPayJoinAsync(Wallet wallet, TransactionInfo transactionInfo, TransactionBroadcaster broadcaster)
		{
			try
			{
				// Do not add the PayJoin client yet, it will be added before broadcasting.
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(wallet, transactionInfo));
				Navigate().To(new TransactionPreviewViewModel(wallet, transactionInfo, broadcaster, txRes));
			}
			catch (InsufficientBalanceException)
			{
				await ShowErrorAsync("Transaction Building", "There are not enough private funds to cover the transaction fee", "Wasabi was unable to create your transaction.");
				Navigate().To(new PrivacyControlViewModel(wallet, transactionInfo, broadcaster));
			}
		}

		private IPayjoinClient? GetPayjoinClient(string endPoint, Config config, HttpClientFactory externalHttpClientFactory)
		{
			if (!string.IsNullOrWhiteSpace(endPoint) &&
				Uri.IsWellFormedUriString(endPoint, UriKind.Absolute))
			{
				var payjoinEndPointUri = new Uri(endPoint);
				if (!config.UseTor)
				{
					if (payjoinEndPointUri.DnsSafeHost.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
					{
						Logger.LogWarning("PayJoin server is an onion service but Tor is disabled. Ignoring...");
						return null;
					}

					if (config.Network == Network.Main && payjoinEndPointUri.Scheme != Uri.UriSchemeHttps)
					{
						Logger.LogWarning("PayJoin server is not exposed as an onion service nor https. Ignoring...");
						return null;
					}
				}

				IHttpClient httpClient = externalHttpClientFactory.NewHttpClient(() => payjoinEndPointUri, isolateStream: false);
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
			else if (AmountBtc > _owner.Wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC))
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
				!AddressStringParser.TryParse(To, _owner.Wallet.Network, out _))
			{
				errors.Add(ErrorSeverity.Error, "Input a valid BTC address or URL.");
			}
			else if (IsPayJoin && _owner.Wallet.KeyManager.IsHardwareWallet)
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

			var wallet = _owner.Wallet;

			if (AddressStringParser.TryParse(text, wallet.Network, out BitcoinUrlBuilder? url))
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

			_owner.Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => ExchangeRate = x)
				.DisposeWith(disposables);

			_owner.Wallet.TransactionProcessor.WhenAnyValue(x => x.Coins)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					PriorLabels.AddRange(x.SelectMany(coin => coin.HdPubKey.Label.Labels));

					PriorLabels = new ObservableCollection<string>(PriorLabels.Distinct());
				})
				.DisposeWith(disposables);

			PriorLabels.AddRange(_owner.Wallet
				.KeyManager
				.GetLabels()
				.Select(x => x.ToString()
					.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
					.SelectMany(x => x));

			PriorLabels = new ObservableCollection<string>(PriorLabels.Distinct());

			var feeProvider = _owner.Wallet.FeeProvider;
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

		private static readonly string[] TestNetXAxisLabels =
		{
			"1w",
			"3d",
			"1d",
			"12h",
			"6h",
			"3h",
			"1h",
			"30m",
			"20m",
			"fastest"
		};

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

			if (_owner.Wallet.Network != Network.TestNet)
			{
				var labels = feeEstimates.Select(x => x.Key)
					.Select(x => FeeTargetTimeConverter.Convert(x, "m", "h", "h", "d", "d"))
					.Reverse()
					.ToArray();

				var xs = feeEstimates.Select(x => (double)x.Key).ToArray();
				var ys = feeEstimates.Select(x => (double)x.Value).ToArray();
#if true
				// GetSmoothValues(xs, ys, out var ts, out var xts);
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
				// GetSmoothValues(TestNetXAxisValues, TestNetYAxisValues, out var ts, out var xts);
				GetSmoothValuesSubdivide(TestNetXAxisValues, TestNetYAxisValues, out var ts, out var xts);
				xAxisValues = ts.ToArray();
				yAxisValues = xts.ToArray();
#else
				xAxisValues = xs.Reverse().ToArray();
				yAxisValues = ys.Reverse().ToArray();
#endif
				// xAxisLabels = TestNetXAxisLabels;
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

		private void GetSmoothValues(double[] xs, double[] ys, out List<double> ts, out List<double> xts)
		{
			var min = xs.Min();
			var max = xs.Max();
			var spline = CubicSpline.InterpolatePchipSorted(xs, ys);

			ts = new List<double>();
			xts = new List<double>();

			for (double t = min; t <= max; t += 1)
			{
				var xt = spline.Interpolate(t);
				ts.Add(t);
				xts.Add(xt);
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
