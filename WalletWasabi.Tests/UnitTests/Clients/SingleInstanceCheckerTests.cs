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
		private static Random Random { get; } = new();

		/// <summary>
		/// Global port may collide when several PRs are being tested on CI at the same time,
		/// so we need some sort of non-determinism here (e.g. random numbers).
		/// </summary>
		private static int GenerateRandomPort()
		{
			return Random.Next(37128, 37168);
		}

		[Fact]
		public async Task SingleInstanceTestsAsync()
		{
			int mainNetPort = GenerateRandomPort();
			int testNetPort = GenerateRandomPort();
			int regTestPort = GenerateRandomPort();

			// Disposal test.
			await using (SingleInstanceChecker sic = new(mainNetPort))
			{
				await sic.CheckAsync();
			}

			// Check different networks.
			await using (SingleInstanceChecker sic = new(mainNetPort))
			{
				await sic.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sic.CheckAsync());

				await using SingleInstanceChecker sicMainNet2 = new(mainNetPort);
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicMainNet2.CheckAsync());

				await using SingleInstanceChecker sicTestNet = new(testNetPort);
				await sicTestNet.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicTestNet.CheckAsync());

				await using SingleInstanceChecker sicRegTest = new(regTestPort);
				await sicRegTest.CheckAsync();
				await Assert.ThrowsAsync<InvalidOperationException>(async () => await sicRegTest.CheckAsync());
			}
		}

		[Fact]
		public async Task OtherInstanceStartedTestsAsync()
		{
			int mainNetPort = GenerateRandomPort();

			// Disposal test.
			await using SingleInstanceChecker sic = new(mainNetPort);
			bool eventCalled = false;

			sic.OtherInstanceStarted += SetCalled;

			try
			{
				// I am the first instance this should be fine.
				await sic.CheckAsync();

				await using SingleInstanceChecker second = new(mainNetPort);

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
	}
}
