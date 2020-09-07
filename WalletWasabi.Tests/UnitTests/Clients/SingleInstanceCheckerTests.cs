using NBitcoin;
using System;
using System.Threading.Tasks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
	[Collection("Serial unit tests collection")]
	public class SingleInstanceCheckerTests
	{
		[Fact]
		public async Task SingleInstanceTestsAsync()
		{
			// Disposal test.
			using (SingleInstanceChecker sic = new SingleInstanceChecker(Network.Main))
			{
				await sic.CheckAsync();
			}

			// Check different networks.
			using (SingleInstanceChecker sic = new SingleInstanceChecker(Network.Main))
			{
				await sic.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sic.CheckAsync());

				using SingleInstanceChecker sicMainNet2 = new SingleInstanceChecker(Network.Main);
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicMainNet2.CheckAsync());

				using SingleInstanceChecker sicTestNet = new SingleInstanceChecker(Network.TestNet);
				await sicTestNet.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicTestNet.CheckAsync());

				using SingleInstanceChecker sicRegTest = new SingleInstanceChecker(Network.RegTest);
				await sicRegTest.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicRegTest.CheckAsync());
			}
		}
	}
}
