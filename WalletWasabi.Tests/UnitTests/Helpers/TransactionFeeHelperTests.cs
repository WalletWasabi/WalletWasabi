using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Fluent.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Helpers;

public class TransactionFeeHelperTests
{
	[Fact]
	public void HandleMissingAndEmptyEstimatesCorrectly()
	{
		// Tests for MainNet.
		{
			// No estimates available.
			{
				Assert.False(TransactionFeeHelper.TryGetFeeEstimates(null, Network.Main, out var actualFeeRateEstimates));
				Assert.Null(actualFeeRateEstimates);
			}

			// Empty estimates.
			{
				Assert.False(TransactionFeeHelper.TryGetFeeEstimates(FeeRateEstimations.Empty, Network.Main, out var actualFeeRateEstimates));
				Assert.Null(actualFeeRateEstimates);
			}

			// Non-empty estimates but all outside the allowed confirmation target range.
			{
				// Specify estimates that are outside the allowed confirmation target range. 
				var sourceData = new Dictionary<int, FeeRate>
				{
					[FeeRateEstimations.AllConfirmationTargets[0] - 1] = new(1m),
					[FeeRateEstimations.AllConfirmationTargets[^1] + 1] = new(1m)
				};

				var feeRateEstimates = new FeeRateEstimations(sourceData);

				// The filtered estimates should be empty since they are outside the allowed confirmation target range.
				Assert.Empty(feeRateEstimates.Estimations);

				// No fee-rate estimates, so nothing should be returned here.
				Assert.False(TransactionFeeHelper.TryGetFeeEstimates(feeRateEstimates, Network.Main, out var actualFeeRateEstimates));
				Assert.Null(actualFeeRateEstimates);
			}
		}

		// Tests for TestNet where a fallback is used to return predefined dummy estimates.
		{
			// Null case.
			{
				Assert.True(TransactionFeeHelper.TryGetFeeEstimates(null, Network.TestNet, out var actualFeeRateEstimates));
				Assert.NotNull(actualFeeRateEstimates);
				Assert.NotEmpty(actualFeeRateEstimates.WildEstimations);
			}

			// Empty case.
			{
				Assert.True(TransactionFeeHelper.TryGetFeeEstimates(FeeRateEstimations.Empty, Network.TestNet, out var actualFeeRateEstimates));
				Assert.NotNull(actualFeeRateEstimates);
				Assert.NotEmpty(actualFeeRateEstimates.WildEstimations);
			}

			// Valid data case: Dummy estimates are returned even in this case!
			{
				var sourceData = new Dictionary<int, FeeRate> { [2] = new(1m) };
				var feeEstimates = new FeeRateEstimations(sourceData);

				Assert.True(TransactionFeeHelper.TryGetFeeEstimates(feeEstimates, Network.TestNet, out var actualFeeRateEstimates));
				Assert.NotNull(actualFeeRateEstimates);

				// The fallback works always, even if some estimates are provided!
				Assert.NotEqual(feeEstimates, actualFeeRateEstimates);
			}
		}
	}

	[Fact]
	public void GoodEstimatesArePreserved()
	{
		var sourceData = new Dictionary<int, FeeRate>
		{
			[FeeRateEstimations.AllConfirmationTargets[0]] = new(1m)
		};

		var expectedFeeRateEstimations = new FeeRateEstimations(sourceData);
		Assert.NotEmpty(expectedFeeRateEstimations.Estimations);

		Assert.True(TransactionFeeHelper.TryGetFeeEstimates(expectedFeeRateEstimations, Network.Main, out var actualFeeRateEstimates));
		Assert.Same(expectedFeeRateEstimations, actualFeeRateEstimates);
	}
}
