using NBitcoin;
using System;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	/// <summary>
	/// The tests in this collection are time-sensitive, therefore this test collection is run in a special way:
	/// Parallel-capable test collections will be run first (in parallel), followed by parallel-disabled test collections (run sequentially) like this one.
	/// </summary>
	/// <seealso href="https://xunit.net/docs/running-tests-in-parallel.html#parallelism-in-test-frameworks"/>
	[Collection("Serial unit tests collection")]
	public class SerialSingleInstanceCheckerTests
	{
		[Fact]
		public void SingleInstanceTests()
		{
			SingleInstanceChecker sic = new SingleInstanceChecker();

			// Disposal test.
			using (var lockHolder = sic.TryAcquireLock(Network.Main))
			{
			}

			// Check different networks.
			using (var _ = sic.TryAcquireLock(Network.Main))
			{
				Assert.Throws<InvalidOperationException>(() =>
				{
					using var _ = sic.TryAcquireLock(Network.Main);
				});
			}

			using (var _ = sic.TryAcquireLock(Network.TestNet))
			{
				Assert.Throws<InvalidOperationException>(() =>
				{
					using var _ = sic.TryAcquireLock(Network.TestNet);
				});
			}

			using (var _ = sic.TryAcquireLock(Network.RegTest)) {
				Assert.Throws<InvalidOperationException>(() =>
				{
					using var _ = sic.TryAcquireLock(Network.RegTest);
				});
			}
		}
	}
}
