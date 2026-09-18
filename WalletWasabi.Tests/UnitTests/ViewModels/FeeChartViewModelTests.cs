using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class FeeChartViewModelTests
{
	[Fact]
	public void EmptyEstimatesDoNotInitializeChart()
	{
		var chart = new FeeChartViewModel(null!);

		Assert.False(chart.TryUpdateFeeEstimates([]));

		Assert.Null(chart.ConfirmationTargetValues);
		Assert.Null(chart.SatoshiPerByteValues);
		Assert.False(chart.TryGetConfirmationTarget(new FeeRate(1m), out _));
	}

	[Fact]
	public void FeeCapBelowEveryEstimateDoesNotInitializeChart()
	{
		var chart = new FeeChartViewModel(null!);

		Assert.False(chart.TryUpdateFeeEstimates(
			[(TimeSpan.FromMinutes(20), new FeeRate(2m))],
			new FeeRate(1m)));

		Assert.Null(chart.ConfirmationTargetValues);
		Assert.Null(chart.SatoshiPerByteValues);
		Assert.False(chart.TryGetConfirmationTarget(new FeeRate(1m), out _));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void UnusableUpdatePreservesSelectionAndAllowsRecovery(bool filteredByFeeCap)
	{
		var chart = CreateChart();
		Assert.True(chart.TryUpdateFeeEstimates(
			[(TimeSpan.FromMinutes(20), new FeeRate(2m)), (TimeSpan.FromMinutes(30), new FeeRate(1m))]));
		chart.SliderValue = chart.SliderMaximum;

		var targets = chart.ConfirmationTargetValues;
		var fees = chart.SatoshiPerByteValues;
		var targetLabels = chart.ConfirmationTargetLabels;
		var feeLabels = chart.SatoshiPerByteLabels;
		var target = chart.CurrentConfirmationTarget;
		var fee = chart.CurrentSatoshiPerByte;
		var targetString = chart.CurrentConfirmationTargetString;
		var sliderValue = chart.SliderValue;
		var sliderMaximum = chart.SliderMaximum;
		var enableCursor = chart.EnableCursor;

		var updated = filteredByFeeCap
			? chart.TryUpdateFeeEstimates([(TimeSpan.FromMinutes(20), new FeeRate(2m))], new FeeRate(1m))
			: chart.TryUpdateFeeEstimates([]);

		Assert.False(updated);
		Assert.Same(targets, chart.ConfirmationTargetValues);
		Assert.Same(fees, chart.SatoshiPerByteValues);
		Assert.Same(targetLabels, chart.ConfirmationTargetLabels);
		Assert.Same(feeLabels, chart.SatoshiPerByteLabels);
		Assert.Equal(target, chart.CurrentConfirmationTarget);
		Assert.Equal(fee, chart.CurrentSatoshiPerByte);
		Assert.Equal(targetString, chart.CurrentConfirmationTargetString);
		Assert.Equal(sliderValue, chart.SliderValue);
		Assert.Equal(sliderMaximum, chart.SliderMaximum);
		Assert.Equal(enableCursor, chart.EnableCursor);

		Assert.True(chart.TryUpdateFeeEstimates(
			[(TimeSpan.FromMinutes(20), new FeeRate(4m)), (TimeSpan.FromMinutes(30), new FeeRate(3m))]));
		Assert.Equal(target, chart.CurrentConfirmationTarget);
		Assert.Equal(3m, chart.CurrentSatoshiPerByte);
		chart.SliderValue = chart.SliderMinimum;
		Assert.Equal(4m, chart.CurrentSatoshiPerByte);
	}

	[Fact]
	public void EmptyChartCanRecoverWhenEstimatesArrive()
	{
		var chart = CreateChart();
		Assert.False(chart.TryUpdateFeeEstimates([]));

		Assert.True(chart.TryUpdateFeeEstimates([(TimeSpan.FromMinutes(20), new FeeRate(1m))]));
		Assert.Equal(1m, chart.CurrentSatoshiPerByte);
		Assert.Equal(2, chart.CurrentConfirmationTarget);
		Assert.True(chart.TryGetConfirmationTarget(new FeeRate(1m), out var target));
		Assert.Equal(2, target);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void SingleAffordableEstimateRendersWithoutCursor(bool includeUnaffordableEstimate)
	{
		var chart = CreateChart();
		(TimeSpan, FeeRate)[] estimates = includeUnaffordableEstimate
			? [(TimeSpan.FromMinutes(20), new FeeRate(2m)), (TimeSpan.FromMinutes(30), new FeeRate(1m))]
			: [(TimeSpan.FromMinutes(30), new FeeRate(1m))];

		Assert.True(chart.TryUpdateFeeEstimates(estimates, new FeeRate(1m)));
		Assert.Equal(new double[] { 3, 3 }, chart.ConfirmationTargetValues);
		Assert.Equal(new double[] { 1, 1 }, chart.SatoshiPerByteValues);
		Assert.False(chart.EnableCursor);
		Assert.Equal(1m, chart.CurrentSatoshiPerByte);
		Assert.True(chart.TryGetConfirmationTarget(new FeeRate(1m), out var target));
		Assert.Equal(3, target);
		chart.SliderValue = chart.SliderMaximum;
		Assert.Equal(1m, chart.CurrentSatoshiPerByte);
	}

	[Fact]
	public void SmoothedEstimatesRespectFeeCapAndSliderSelection()
	{
		var estimates = new FeeRateEstimations(new Dictionary<int, FeeRate>
		{
			[2] = new(102m),
			[3] = new(20m),
			[6] = new(10m),
			[18] = new(1m)
		});
		var chart = CreateChart();

		Assert.True(chart.TryUpdateFeeEstimates(estimates.WildEstimations, new FeeRate(15m)));
		Assert.NotNull(chart.SatoshiPerByteValues);
		Assert.NotNull(chart.ConfirmationTargetValues);
		Assert.Equal(chart.SatoshiPerByteValues.Length, chart.ConfirmationTargetValues.Length);
		Assert.All(chart.SatoshiPerByteValues, fee => Assert.InRange(fee, 1, 15));
		Assert.InRange(chart.CurrentSatoshiPerByte, 1m, 15m);
		Assert.True(chart.EnableCursor);
		chart.SliderValue = chart.SliderMaximum;
		Assert.Equal(1m, chart.CurrentSatoshiPerByte);
		chart.SliderValue = chart.SliderMinimum;
		Assert.InRange(chart.CurrentSatoshiPerByte, 10m, 15m);
		Assert.NotEmpty(chart.CurrentConfirmationTargetString);
	}

	// A preselected target keeps these chart tests independent of UI services.
	private static FeeChartViewModel CreateChart() => new(null!) { CurrentConfirmationTarget = 2 };
}
