using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Exceptions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.MathNet;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui.Converters;
using WalletWasabi.Logging;
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
	public partial class SendFeeViewModel : RoutableViewModel
	{
		private readonly Wallet _wallet;
		private readonly TransactionInfo _transactionInfo;
		[AutoNotify] private double[]? _confirmationTargets;
		[AutoNotify] private double[]? _yAxisValues;
		[AutoNotify] private string[] _confirmationTargetLabels;
		[AutoNotify] private double _currentConfirmationTarget = 36;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private int _sliderMinimum = 0;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private int _sliderMaximum = 9;
		[AutoNotify] private int _sliderValue;
		private bool _updatingCurrentValue;
		private FeeRate _feeRate;
		private double _lastConfirmationTarget;

		public SendFeeViewModel(Wallet wallet, TransactionInfo transactionInfo)
		{
			_wallet = wallet;
			_transactionInfo = transactionInfo;

			_lastConfirmationTarget = _currentConfirmationTarget;

			this.WhenAnyValue(x => x.CurrentConfirmationTarget)
				.Subscribe(x =>
				{
					if (x > 0)
					{
						_feeRate = new FeeRate(GetSatoshiPerByte(x));
						SetSliderValue(x);
					}
				});

			this.WhenAnyValue(x => x.SliderValue)
				.Subscribe(SetXAxisCurrentValue);

			NextCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				_lastConfirmationTarget = CurrentConfirmationTarget;
				_transactionInfo.ConfirmationTimeSpan = CalculateConfirmationTime(_lastConfirmationTarget);

				await OnNextAsync();
			});
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
				if (_transactionInfo.PayJoinClient is { })
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
				await ShowErrorAsync("Transaction Building", ex.ToUserFriendlyString(),
					"Wasabi was unable to create your transaction.");
			}
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

		private async Task BuildTransactionAsNormalAsync(TransactionInfo transactionInfo, Money totalMixedCoinsAmount)
		{
			try
			{
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo));
				Navigate().To(new OptimisePrivacyViewModel(_wallet, transactionInfo, txRes));
			}
			catch (InsufficientBalanceException)
			{
				var txRes = await Task.Run(() => TransactionHelpers.BuildTransaction(_wallet, transactionInfo.Address,
					totalMixedCoinsAmount, transactionInfo.Labels, transactionInfo.FeeRate, transactionInfo.Coins,
					subtractFee: true));
				var dialog = new InsufficientBalanceDialogViewModel(BalanceType.Private, txRes,
					_wallet.Synchronizer.UsdExchangeRate);
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
				await ShowErrorAsync("Transaction Building",
					"There are not enough private funds to cover the transaction fee",
					"Wasabi was unable to create your transaction.");
				Navigate().To(new PrivacyControlViewModel(_wallet, transactionInfo));
			}
		}

		private TimeSpan CalculateConfirmationTime(double targetBlock)
		{
			var timeInMinutes = Math.Ceiling(targetBlock) * 10;
			var time = TimeSpan.FromMinutes(timeInMinutes);
			return time;
		}

		private void SetSliderValue(double xAxisCurrentValue)
		{
			if (!_updatingCurrentValue)
			{
				_updatingCurrentValue = true;
				if (_confirmationTargets is not null)
				{
					SliderValue = GetSliderValue(xAxisCurrentValue, _confirmationTargets);
				}

				_updatingCurrentValue = false;
			}
		}

		private void SetXAxisCurrentValue(int sliderValue)
		{
			if (_confirmationTargets is not null)
			{
				if (!_updatingCurrentValue)
				{
					_updatingCurrentValue = true;
					var index = _confirmationTargets.Length - sliderValue - 1;
					CurrentConfirmationTarget = _confirmationTargets[index];
					_updatingCurrentValue = false;
				}
			}
		}

		private void UpdateFeeEstimates(Dictionary<int, int> feeEstimates)
		{
			string[] confirmationTargetLabels;
			double[] confirmationTargets;
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
				confirmationTargets = ts.ToArray();
				yAxisValues = xts.ToArray();
#else
				confirmationTargets = xs.Reverse().ToArray();
				yAxisValues = ys.Reverse().ToArray();
#endif
				confirmationTargetLabels = labels;
			}
			else
			{
#if true
				GetSmoothValuesSubdivide(TestNetXAxisValues, TestNetYAxisValues, out var ts, out var xts);
				confirmationTargets = ts.ToArray();
				yAxisValues = xts.ToArray();
#else
				confirmationTargets = xs.Reverse().ToArray();
				yAxisValues = ys.Reverse().ToArray();
#endif
				var labels = TestNetXAxisValues.Select(x => x)
					.Select(x => FeeTargetTimeConverter.Convert((int)x, "m", "h", "h", "d", "d"))
					.Reverse()
					.ToArray();
				confirmationTargetLabels = labels;
			}

			_updatingCurrentValue = true;
			ConfirmationTargetLabels = confirmationTargetLabels;
			ConfirmationTargets = confirmationTargets;
			YAxisValues = yAxisValues;
			SliderMinimum = 0;
			SliderMaximum = confirmationTargets.Length - 1;
			CurrentConfirmationTarget = Math.Clamp(CurrentConfirmationTarget, ConfirmationTargets.Min(), ConfirmationTargets.Max());
			SliderValue = GetSliderValue(CurrentConfirmationTarget, ConfirmationTargets);
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

		private int GetSliderValue(double xAxisCurrentValue, double[] xAxisValues)
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

		private void GetSmoothValuesSubdivide(double[] xs, double[] ys, out List<double> xts, out List<double> yts)
		{
			const int Divisions = 256;

			xts = new List<double>();
			yts = new List<double>();

			if (xs.Length > 2)
			{
				var spline = CubicSpline.InterpolatePchipSorted(xs, ys);

				for (var i = 0; i < xs.Length - 1; i++)
				{
					var a = xs[i];
					var b = xs[i + 1];
					var range = b - a;
					var step = range / Divisions;

					var x0 = xs[i];
					xts.Add(x0);
					var yt0 = spline.Interpolate(xs[i]);
					yts.Add(yt0);

					for (var xt = a + step; xt < b; xt += step)
					{
						var yt = spline.Interpolate(xt);
						xts.Add(xt);
						yts.Add(yt);
					}
				}

				var xn = xs[^1];
				xts.Add(xn);
				var yn = spline.Interpolate(xs[^1]);
				yts.Add(yn);
			}
			else
			{
				for (var i = 0; i < xs.Length; i++)
				{
					xts.Add(xs[i]);
					yts.Add(ys[i]);
				}
			}

			xts.Reverse();
			yts.Reverse();
		}

		private decimal GetSatoshiPerByte(double t)
		{
			if (_confirmationTargets is { } && _yAxisValues is { })
			{
				var confirmationTargetsReversed = _confirmationTargets.Reverse().ToArray();
				var y = _yAxisValues.Reverse().ToArray();

				if (confirmationTargetsReversed.Length > 2)
				{
					var spline = CubicSpline.InterpolatePchipSorted(confirmationTargetsReversed, y);
					var interpolated = (decimal) spline.Interpolate(t);
					return Math.Clamp(interpolated, (decimal) y[^1], (decimal) y[0]);
				}

				if (confirmationTargetsReversed.Length == 2)
				{
					if (confirmationTargetsReversed[1] - confirmationTargetsReversed[0] == 0.0)
					{
						return (decimal) y[0];
					}
					var slope = (y[1] - y[0]) / (confirmationTargetsReversed[1] - confirmationTargetsReversed[0]);
					var interpolated = (decimal)(y[0] + (t - confirmationTargetsReversed[0]) * slope);
					return Math.Clamp(interpolated, (decimal) y[^1], (decimal) y[0]);
				}

				if (confirmationTargetsReversed.Length == 1)
				{
					return (decimal)y[0];
				}
			}

			return SliderMaximum;
		}
	}
}