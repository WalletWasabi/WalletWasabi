using NBitcoin;
using System;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet
{
	public class LockTimeSelectorTests
	{
		[Fact]
		public void GetLockTimeBasedOnDistributionTest()
		{
			var lockTimeSelector = new LockTimeSelector(new Random());

			uint tipHeight = 600_000;
			LockTime lockTime = lockTimeSelector.GetLockTimeBasedOnDistribution(tipHeight);

			if (lockTime.Value == 0)
			{
				Assert.Equal(LockTime.Zero, lockTime);
			}
			else
			{
				Assert.InRange(lockTime.Value, tipHeight - 99, tipHeight + 1);
			}
		}
	}
}
