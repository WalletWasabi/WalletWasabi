using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Fluent.MathNet;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "wallet_action_send",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.HomeScreen)]
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
		[AutoNotify] private double  _xAxisCurrentValue = 36;

		private string? _payJoinEndPoint;
		private bool _parsingUrl;

		public SendViewModel(WalletViewModel walletVm)
		{
			_to = "";
			_owner = walletVm;
			_transactionInfo = new TransactionInfo();
			_labels = new ObservableCollection<string>();

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
						_transactionInfo.FeeRate = new FeeRate(GetYAxisValueFromXAxisCurrentValue(x));
					}
				});

			Labels.ToObservableChangeSet().Subscribe(x =>
			{
				_transactionInfo.Labels = new SmartLabel(_labels.ToArray());
			});

			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				var text = await Application.Current.Clipboard.GetTextAsync();

				_parsingUrl = true;

				if (!TryParseUrl(text))
				{
					To = text;
					// todo validation errors.
				}

				_parsingUrl = false;
			});

			NextCommand = ReactiveCommand.Create(() =>
			{
				var transactionInfo = _transactionInfo;
				var wallet = _owner.Wallet;
				var targetAnonymitySet = wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();
				var mixedCoins = wallet.Coins.Where(x => x.HdPubKey.AnonymitySet >= targetAnonymitySet).ToList();

				if (mixedCoins.Any())
				{
					var intent = new PaymentIntent(
						destination: transactionInfo.Address,
						amount: transactionInfo.Amount,
						subtractFee: false,
						label: transactionInfo.Labels);

					try
					{
						var txRes = wallet.BuildTransaction(
							wallet.Kitchen.SaltSoup(),
							intent,
							FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
							allowUnconfirmed: true,
							mixedCoins.Select(x => x.OutPoint));

						// Private coins are enough.
						Navigate().To(new OptimisePrivacyViewModel(wallet, transactionInfo, walletVm.TransactionBroadcaster, txRes));
						return;
					}
					catch (NotEnoughFundsException)
					{
						// Do Nothing
					}
				}

				Navigate().To(new PrivacyControlViewModel(wallet, transactionInfo));
			}, this.WhenAnyValue(x=>x.Labels.Count).Any());
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
				!AddressStringParser.TryParse(To, _owner.Wallet.Network, out BitcoinUrlBuilder? url))
			{
				errors.Add(ErrorSeverity.Error, "Input a valid BTC address or URL.");
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

				if (url.UnknowParameters.TryGetValue("pj", out var endPoint))
				{
					if (!wallet.KeyManager.IsHardwareWallet)
					{
						_payJoinEndPoint = endPoint;
					}
					else
					{
						// Validation error... "Payjoin not available! for hw wallets."
					}
				}
				else
				{
					_payJoinEndPoint = null;
				}
			}
			else
			{
				IsFixedAmount = false;
				_payJoinEndPoint = null;
			}

			IsPayJoin = _payJoinEndPoint is { };

			return result;
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

			_owner.Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => ExchangeRate = x)
				.DisposeWith(disposables);

			_owner.Wallet.TransactionProcessor.WhenAnyValue(x => x.Coins)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					PriorLabels.Clear();
					PriorLabels.AddRange(x.SelectMany(coin => coin.HdPubKey.Label.Labels).Distinct());
				})
				.DisposeWith(disposables);

			_owner.Wallet.Synchronizer.WhenAnyValue(x => x.AllFeeEstimate)
				.Where(x => x is { })
				.Select(x => x!.Estimations)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(UpdateFeeEstimates)
				.DisposeWith(disposables);

			if (_owner.Wallet.Synchronizer.AllFeeEstimate is { })
			{
				UpdateFeeEstimates(_owner.Wallet.Synchronizer.AllFeeEstimate.Estimations);
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
				xAxisLabels = TestNetXAxisLabels;
			}

			XAxisLabels = xAxisLabels;
			XAxisValues = xAxisValues;
			YAxisValues = yAxisValues;
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
				var interpolated = (decimal) spline.Interpolate(t);
				return Math.Clamp(interpolated, (decimal)y[^1], (decimal)y[0]);
			}

			return (decimal)XAxisMaxValue;
		}

		public ICommand PasteCommand { get; }

		public double XAxisMinValue { get; set; } = 1;

		public double XAxisMaxValue { get; set; } = 1008;
	}
}
