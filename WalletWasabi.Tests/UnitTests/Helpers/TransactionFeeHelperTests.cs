using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class TransactionFeeHelperTests
{
	[Fact]
	public void MissingEstimatesAreUnavailable()
	{
		Assert.False(TransactionFeeHelper.TryGetFeeEstimates(null, Network.Main, out var estimates));
		Assert.Null(estimates);
	}

	[Fact]
	public void EmptyEstimatesAreUnavailable()
	{
		Assert.False(TransactionFeeHelper.TryGetFeeEstimates(FeeRateEstimations.Empty, Network.Main, out var estimates));
		Assert.Null(estimates);
	}

	[Fact]
	public void FilteredOutEstimatesAreUnavailable()
	{
		var feeEstimates = new FeeRateEstimations(new Dictionary<int, FeeRate>
		{
			[0] = new(1m),
			[1009] = new(1m)
		});

		Assert.False(TransactionFeeHelper.TryGetFeeEstimates(feeEstimates, Network.Main, out var estimates));
		Assert.Null(estimates);
	}

	[Fact]
	public void AvailableEstimatesArePreserved()
	{
		var feeEstimates = new FeeRateEstimations(new Dictionary<int, FeeRate> { [2] = new(1m) });

		Assert.True(TransactionFeeHelper.TryGetFeeEstimates(feeEstimates, Network.Main, out var estimates));
		Assert.Same(feeEstimates, estimates);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void TestNetRetainsFallbackEstimates(bool hasEmptyEstimates)
	{
		var feeEstimates = hasEmptyEstimates ? FeeRateEstimations.Empty : null;

		Assert.True(TransactionFeeHelper.TryGetFeeEstimates(feeEstimates, Network.TestNet, out var estimates));
		Assert.NotNull(estimates);
		Assert.NotEmpty(estimates.WildEstimations);
	}
}
