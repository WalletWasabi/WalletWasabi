using NBitcoin;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets;

public class LockTimeSelectorTests
{
	[Theory]
	[InlineData(0.0, 0u)]
	[InlineData(0.8999, 0u)]
	[InlineData(0.9, 600_000u)]
	[InlineData(0.9749, 600_000u)]
	[InlineData(0.975, 600_000u)]
	[InlineData(0.9814, 600_000u)]
	[InlineData(0.9815, 599_901u)]
	[InlineData(0.9999, 599_901u)]
	public void GetLockTimeBasedOnDistributionTest(double randomValue, uint expectedHeight)
	{
		var lockTimeSelector = new LockTimeSelector(new FixedRandom(randomValue));

		LockTime lockTime = lockTimeSelector.GetLockTimeBasedOnDistribution(600_000);

		Assert.Equal(expectedHeight, lockTime.Value);
	}

	[Theory]
	[InlineData(0u)]
	[InlineData(1u)]
	[InlineData(98u)]
	[InlineData(99u)]
	[InlineData(100u)]
	public void RecentLockTimeDoesNotUnderflow(uint tipHeight)
	{
		var lockTimeSelector = new LockTimeSelector(new FixedRandom(0.99));

		LockTime lockTime = lockTimeSelector.GetLockTimeBasedOnDistribution(tipHeight);

		Assert.Equal((uint)Math.Max(0, (long)tipHeight - 99), lockTime.Value);
		Assert.InRange(lockTime.Value, 0u, tipHeight);
	}

	private sealed class FixedRandom(double value) : Random
	{
		public override double NextDouble() => value;
		public override int Next(int minValue, int maxValue) => maxValue - 1;
	}
}
