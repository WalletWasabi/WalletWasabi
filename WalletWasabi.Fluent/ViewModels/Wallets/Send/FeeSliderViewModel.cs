using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Fluent.MathNet;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(
		Title = "Send",
		Caption = "",
		IconName = "wallet_action_send",
		NavBarPosition = NavBarPosition.None,
		Searchable = false,
		NavigationTarget = NavigationTarget.DialogScreen)]
	public partial class FeeSliderViewModel : DialogViewModelBase<(FeeRate feeRate, TimeSpan confirmationTime)>
	{
		private readonly Wallet _wallet;

		[AutoNotify] private double[] _xAxisValues;
		[AutoNotify] private double[] _yAxisValues;
		[AutoNotify] private string[] _xAxisLabels;
		[AutoNotify] private double _xAxisCurrentValue = 36;
		[AutoNotify] private int _xAxisCurrentValueIndex;

		[AutoNotify(SetterModifier = AccessModifier.Private)]
		private int _xAxisMinValue = 0;

		[AutoNotify(SetterModifier = AccessModifier.Private)]
		private int _xAxisMaxValue = 9;

		private bool _updatingCurrentValue;
		private FeeRate _feeRate;
		private double _lastXAxisCurrentValue;

		public FeeSliderViewModel(Wallet wallet)
		{
			_wallet = wallet;
			_lastXAxisCurrentValue = _xAxisCurrentValue;

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

			NextCommand = ReactiveCommand.Create(() =>
			{
				_lastXAxisCurrentValue = XAxisCurrentValue;
				var confirmationTime = CalculateConfirmationTime(_lastXAxisCurrentValue);

				Close(DialogResultKind.Normal, (_feeRate, confirmationTime));
			});
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

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