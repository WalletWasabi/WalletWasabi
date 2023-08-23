using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.MathNet;
using System.Windows.Input;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class FeeChartViewModel : ViewModelBase
{
	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private int _sliderMinimum;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private int _sliderMaximum;

	[AutoNotify] private int _sliderValue;
	[AutoNotify] private string[]? _satoshiPerByteLabels;
	[AutoNotify] private double[]? _satoshiPerByteValues;
	[AutoNotify] private double[]? _confirmationTargetValues;
	[AutoNotify] private string[]? _confirmationTargetLabels;
	[AutoNotify] private double _currentConfirmationTarget = -1;
	[AutoNotify] private decimal _currentSatoshiPerByte;
	[AutoNotify] private string _currentConfirmationTargetString;
	[AutoNotify] private bool _enableCursor = true;
	private bool _updatingCurrentValue;

	public FeeChartViewModel()
	{
		_sliderMinimum = 0;
		_sliderMaximum = 9;
		_currentConfirmationTargetString = "";

		this.WhenAnyValue(x => x.CurrentConfirmationTarget)
			.Subscribe(x =>
			{
				if (x > 0)
				{
					SetSliderValue(x);
				}
			});

		this.WhenAnyValue(x => x.SliderValue)
			.Subscribe(SetXAxisCurrentValue);

		MoveSliderRightCommand = ReactiveCommand.Create(() => SliderValue = Math.Max(SliderMinimum, SliderValue - 10));
		MoveSliderLeftCommand = ReactiveCommand.Create(() => SliderValue = Math.Min(SliderMaximum, SliderValue + 10));
	}

	public ICommand MoveSliderRightCommand { get; }
	public ICommand MoveSliderLeftCommand { get; }

	private void UpdateFeeAndEstimate(double confirmationTarget)
	{
		CurrentSatoshiPerByte = GetSatoshiPerByte(confirmationTarget);
		var targetBlock = (int)Math.Ceiling(confirmationTarget);
		var estimatedTime = TransactionFeeHelper.CalculateConfirmationTime(targetBlock);
		CurrentConfirmationTargetString = ConfirmationTimeLabel.SliderLabel(estimatedTime);
	}

	private void SetSliderValue(double confirmationTarget)
	{
		if (!_updatingCurrentValue)
		{
			_updatingCurrentValue = true;
			if (_confirmationTargetValues is not null)
			{
				SliderValue = GetSliderValue(confirmationTarget, _confirmationTargetValues);
				UpdateFeeAndEstimate(confirmationTarget);
			}

			_updatingCurrentValue = false;
		}
	}

	private void SetXAxisCurrentValue(int sliderValue)
	{
		if (_confirmationTargetValues is not null)
		{
			if (!_updatingCurrentValue)
			{
				_updatingCurrentValue = true;
				var index = _confirmationTargetValues.Length - sliderValue - 1;
				CurrentConfirmationTarget = _confirmationTargetValues[index];
				UpdateFeeAndEstimate(CurrentConfirmationTarget);
				_updatingCurrentValue = false;
			}
		}
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

	public decimal GetSatoshiPerByte(double t)
	{
		if (_confirmationTargetValues is { } && _satoshiPerByteValues is { })
		{
			var xs = _confirmationTargetValues.Reverse().ToArray();
			var ys = _satoshiPerByteValues.Reverse().ToArray();

			if (xs.Length > 2)
			{
				var spline = CubicSpline.InterpolatePchipSorted(xs, ys);
				var interpolated = (decimal)spline.Interpolate(t);
				return Math.Clamp(interpolated, (decimal)ys[^1], (decimal)ys[0]);
			}

			if (xs.Length == 2)
			{
				if (xs[1] - xs[0] == 0.0)
				{
					return (decimal)ys[0];
				}

				var slope = (ys[1] - ys[0]) / (xs[1] - xs[0]);
				var interpolated = (decimal)(ys[0] + (t - xs[0]) * slope);
				return Math.Clamp(interpolated, (decimal)ys[^1], (decimal)ys[0]);
			}

			if (xs.Length == 1)
			{
				return (decimal)ys[0];
			}
		}

		return SliderMaximum;
	}

	public bool TryGetConfirmationTarget(FeeRate feeRate, out double target)
	{
		target = -1;

		if (SatoshiPerByteValues is null || ConfirmationTargetValues is null) // Should not happen
		{
			return false;
		}

		try
		{
			var closestValue = SatoshiPerByteValues.Last(x => new FeeRate((decimal)x) <= feeRate);
			var indexOfClosestValue = SatoshiPerByteValues.LastIndexOf(closestValue);

			target = ConfirmationTargetValues[indexOfClosestValue];
		}
		catch (Exception)
		{
			// Ignored.
		}

		return target > -1;
	}

	private int GetSliderValue(double x, double[] xs)
	{
		for (var i = 0; i < xs.Length; i++)
		{
			if (xs[i] <= x)
			{
				var index = xs.Length - i - 1;
				return index;
			}
		}

		return 0;
	}

	public void UpdateFeeEstimates(Dictionary<int, int> feeEstimates, FeeRate? maxFee = null)
	{
		var enableCursor = true;
		var areAllValuesEqual = AreEstimatedFeeRatesEqual(feeEstimates);
		var correctedFeeEstimates = areAllValuesEqual ? feeEstimates : DistinctByValues(feeEstimates);

		var xs = correctedFeeEstimates.Select(x => (double)x.Key).ToArray();
		var ys = correctedFeeEstimates.Select(x => (double)x.Value).ToArray();

		List<double>? xts;
		List<double>? yts;
		if (xs.Length == 1)
		{
			xs = new[] { xs[0], xs[0] };
			ys = new[] { ys[0], ys[0] };
			xts = xs.ToList();
			yts = ys.ToList();
			enableCursor = false;
		}
		else
		{
			GetSmoothValuesSubdivide(xs, ys, out xts, out yts);
		}

		if (maxFee is { })
		{
			RemoveOverpaymentValues(xts, yts, (double)maxFee.SatoshiPerByte);
		}

		var confirmationTargetValues = xts.ToArray();
		var confirmationTargetLabels = GetConfirmationTargetLabels(xts).ToArray();
		var satoshiPerByteValues = yts.ToArray();

		_updatingCurrentValue = true;

		if (satoshiPerByteValues.Any())
		{
			var maxY = satoshiPerByteValues.Max();
			var minY = satoshiPerByteValues.Min();

			SatoshiPerByteLabels = areAllValuesEqual
				? new[] { "", "", maxY.ToString("F0") }
				: new[] { minY.ToString("F0"), ((maxY + minY) / 2).ToString("F0"), maxY.ToString("F0") };
		}
		else
		{
			SatoshiPerByteLabels = null;
		}

		ConfirmationTargetLabels = confirmationTargetLabels;
		ConfirmationTargetValues = confirmationTargetValues;
		SatoshiPerByteValues = satoshiPerByteValues;

		SliderMinimum = 0;
		SliderMaximum = confirmationTargetValues.Length - 1;

		var confirmationTargetCandidate = CurrentConfirmationTarget < 0
			? ConfirmationTargetValues.MinBy(x => Math.Abs(x - Services.UiConfig.FeeTarget))
			: CurrentConfirmationTarget;

		CurrentConfirmationTarget = Math.Clamp(confirmationTargetCandidate, ConfirmationTargetValues.Min(), ConfirmationTargetValues.Max());
		SliderValue = GetSliderValue(CurrentConfirmationTarget, ConfirmationTargetValues);
		UpdateFeeAndEstimate(CurrentConfirmationTarget);

		EnableCursor = enableCursor;

		_updatingCurrentValue = false;
	}

	private void RemoveOverpaymentValues(List<double> xts, List<double> yts, double maxFeeSatoshiPerByte)
	{
		for (var i = yts.Count - 1; i >= 0; i--)
		{
			if (yts[i] > maxFeeSatoshiPerByte)
			{
				yts.RemoveAt(i);
				xts.RemoveAt(i);
			}
			else
			{
				break;
			}
		}
	}

	private IEnumerable<string> GetConfirmationTargetLabels(List<double> confirmationTargets)
	{
		var blockTargetCount = confirmationTargets.Count;
		var interval = blockTargetCount >= 5 ? blockTargetCount / 5 : 1;

		for (int i = 0; i < blockTargetCount; i += interval)
		{
			var targetBlock = (int)Math.Ceiling(confirmationTargets[i]);
			var label = ConfirmationTimeLabel.AxisLabel(TransactionFeeHelper.CalculateConfirmationTime(targetBlock));

			if (i + interval <= blockTargetCount)
			{
				yield return label;
			}
		}

		if (interval != 1)
		{
			var targetBlock = (int)Math.Ceiling(confirmationTargets.Last());
			yield return ConfirmationTimeLabel.AxisLabel(TransactionFeeHelper.CalculateConfirmationTime(targetBlock));
		}
	}

	public void InitCurrentConfirmationTarget(FeeRate feeRate)
	{
		CurrentConfirmationTarget = TryGetConfirmationTarget(feeRate, out var target) ? target : 1;
	}

	public Dictionary<double, double> GetValues()
	{
		Dictionary<double, double> values = new();

		if (ConfirmationTargetValues is null || SatoshiPerByteValues is null)
		{
			return values;
		}

		if (ConfirmationTargetValues.Length != SatoshiPerByteValues.Length)
		{
			throw new InvalidDataException("The count of X and Y values are not equal!");
		}

		var numberOfItems = ConfirmationTargetValues.Length;

		for (var i = 0; i < numberOfItems; i++)
		{
			var blockTarget = ConfirmationTargetValues[i];
			var satPerByte = SatoshiPerByteValues[i];

			values.Add(blockTarget, satPerByte);
		}

		return values;
	}

	private Dictionary<int, int> DistinctByValues(Dictionary<int, int> feeEstimates)
	{
		Dictionary<int, int> valuesToReturn = new();

		foreach (var estimate in feeEstimates)
		{
			var similarBlockTargets = feeEstimates.Where(x => x.Value == estimate.Value);
			var fasterSimilarBlockTarget = similarBlockTargets.First();

			if (fasterSimilarBlockTarget.Key == estimate.Key)
			{
				valuesToReturn.Add(estimate.Key, estimate.Value);
			}
		}

		return valuesToReturn;
	}

	private bool AreEstimatedFeeRatesEqual(Dictionary<int, int> feeEstimates)
	{
		var first = feeEstimates.First();
		var last = feeEstimates.Last();

		return first.Value == last.Value;
	}
}
