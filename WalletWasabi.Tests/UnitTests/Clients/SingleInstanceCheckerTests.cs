using NBitcoin;
using System;
using System.Threading;
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
			await using (SingleInstanceChecker sic = new SingleInstanceChecker(Network.Main, mainLockName))
			{
				await sic.CheckAsync();
			}

			// Check different networks.
			await using (SingleInstanceChecker sic = new SingleInstanceChecker(Network.Main, mainLockName))
			{
				await sic.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sic.CheckAsync());

				await using SingleInstanceChecker sicMainNet2 = new SingleInstanceChecker(Network.Main, mainLockName);
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicMainNet2.CheckAsync());

				string testnetLockName = GenerateLockName(Network.TestNet);
				await using SingleInstanceChecker sicTestNet = new SingleInstanceChecker(Network.TestNet, testnetLockName);
				await sicTestNet.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicTestNet.CheckAsync());

				string regtestLockName = GenerateLockName(Network.RegTest);
				await using SingleInstanceChecker sicRegTest = new SingleInstanceChecker(Network.RegTest, regtestLockName);
				await sicRegTest.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicRegTest.CheckAsync());
			}
		}

		[Fact]
		public async Task OtherInstanceStartedTestsAsync()
		{
			string mainLockName = GenerateLockName(Network.Main);

			// Disposal test.
			await using var sic = new SingleInstanceChecker(Network.Main, mainLockName);
			bool eventCalled = false;

			sic.OtherInstanceStarted += SetCalled;

			try
			{
				// I am the first instance this should be fine.
				await sic.CheckAsync();

				await using var second = new SingleInstanceChecker(Network.Main, mainLockName);

				// I am the second one.
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await second.CheckAsync());

				Assert.True(eventCalled);
			}
			finally
			{
				sic.OtherInstanceStarted -= SetCalled;
			}

			void SetCalled(object? sender, EventArgs args)
			{
				eventCalled = true;
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
