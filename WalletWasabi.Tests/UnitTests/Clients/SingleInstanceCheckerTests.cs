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
			string mainLockName = GenerateLockName(Network.Main);

			// Disposal test.
			using (SingleInstanceChecker sic = new SingleInstanceChecker(Network.Main, mainLockName))
			{
				await sic.CheckAsync();
			}

			// Check different networks.
			using (SingleInstanceChecker sic = new SingleInstanceChecker(Network.Main, mainLockName))
			{
				await sic.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sic.CheckAsync());

				using SingleInstanceChecker sicMainNet2 = new SingleInstanceChecker(Network.Main, mainLockName);
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicMainNet2.CheckAsync());

				string testnetLockName = GenerateLockName(Network.TestNet);
				using SingleInstanceChecker sicTestNet = new SingleInstanceChecker(Network.TestNet, testnetLockName);
				await sicTestNet.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicTestNet.CheckAsync());

				string regtestLockName = GenerateLockName(Network.RegTest);
				using SingleInstanceChecker sicRegTest = new SingleInstanceChecker(Network.RegTest, regtestLockName);
				await sicRegTest.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicRegTest.CheckAsync());
			}
		}

		/// <summary>
		/// Global lock names may collide when several PRs are being tested on CI at the same time,
		/// so we need some sort of non-determinism here (e.g. random numbers).
		/// </summary>
		private static string GenerateLockName(Network network)
		{
			return $"{network}-{new Random().Next(1_000_000)}";
		}
	}
}
