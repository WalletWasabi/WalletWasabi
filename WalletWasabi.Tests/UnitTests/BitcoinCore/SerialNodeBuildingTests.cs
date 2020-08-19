using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	/// <summary>
	/// The test in this collection is time-sensitive, therefore this test collection is run in a special way:
	/// Parallel-capable test collections will be run first (in parallel), followed by this parallel-disabled test collections (run sequentially).
	/// </summary>
	/// <seealso href="https://xunit.net/docs/running-tests-in-parallel.html#parallelism-in-test-frameworks"/>
	[Collection("Serial unit tests collection")]
	public class SerialNodeBuildingTests
	{
		[Fact]
		public async Task GetNodeVersionTestsAsync()
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			Version version = await CoreNode.GetVersionAsync(cts.Token);
			Assert.Equal(WalletWasabi.Helpers.Constants.BitcoinCoreVersion, version);
		}
	}
}
