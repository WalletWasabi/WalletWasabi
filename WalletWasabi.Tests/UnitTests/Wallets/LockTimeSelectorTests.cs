using NBitcoin;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets;

public class LockTimeSelectorTests
{
	[Theory]
	[InlineData(600_000u, 0.978)]
	[InlineData(0u, 0.99)]
	[InlineData(10u, 0.99)]
	[InlineData(600_000u, 0.99)]
	public void ImmediatePaymentsNeverSelectAFutureLockTime(uint tipHeight, double sample)
	{
		var selected = new LockTimeSelector(new FixedRandom(sample)).GetLockTimeBasedOnDistribution(tipHeight);
		Assert.True(selected.Value <= tipHeight, $"Locktime {selected.Value} is not final for the next block after {tipHeight}.");
	}

	private sealed class FixedRandom(double sample) : Random
	{
		public override double NextDouble() => sample;
		public override int Next(int minValue, int maxValue) => maxValue - 1;
	}

	[Fact]
	public void GetLockTimeBasedOnDistributionTest()
	{
		var lockTimeSelector = new LockTimeSelector(Random.Shared);

		uint tipHeight = 600_000;
		LockTime lockTime = lockTimeSelector.GetLockTimeBasedOnDistribution(tipHeight);

		if (lockTime.Value == 0)
		{
			Assert.Equal(LockTime.Zero, lockTime);
		}
		else
		{
			Assert.InRange(lockTime.Value, tipHeight - 99, tipHeight);
		}
	}
}
