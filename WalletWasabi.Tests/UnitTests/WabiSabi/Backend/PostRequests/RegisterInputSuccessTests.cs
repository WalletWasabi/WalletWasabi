using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests
{
	public class RegisterInputSuccessTests
	{
		private static void AssertSingleAliceSuccessfullyRegistered(Round round, DateTimeOffset minAliceDeadline, ArenaResponse<uint256> resp)
		{
			var alice = Assert.Single(round.Alices);
			Assert.NotNull(resp);
			Assert.NotNull(resp.RealAmountCredentials);
			Assert.NotNull(resp.RealVsizeCredentials);
			Assert.True(minAliceDeadline <= alice.Deadline);
		}

		[Fact]
		public async Task SuccessAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			using Key key = new();
			var coin = WabiSabiFactory.CreateCoin(key);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, WabiSabiFactory.CreatePreconfiguredRpcClient(coin), round);

			var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
			var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
			var resp = await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, key, CancellationToken.None).ConfigureAwait(false);
			AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task SuccessWithAliceUpdateIntraRoundAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			using Key key = new();
			var coin = WabiSabiFactory.CreateCoin(key);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, WabiSabiFactory.CreatePreconfiguredRpcClient(coin), round);

			// Make sure an Alice have already been registered with the same input.
			var preAlice = WabiSabiFactory.CreateAlice(coin, WabiSabiFactory.CreateOwnershipProof(key));
			round.Alices.Add(preAlice);

			var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
			var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
			var resp = await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, key, CancellationToken.None).ConfigureAwait(false);
			AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task SuccessWithAliceUpdateCrossRoundAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var anotherRound = WabiSabiFactory.CreateRound(cfg);

			using Key key = new();
			var coin = WabiSabiFactory.CreateCoin(key);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, WabiSabiFactory.CreatePreconfiguredRpcClient(coin), round, anotherRound);

			// Make sure an Alice have already been registered with the same input.
			var preAlice = WabiSabiFactory.CreateAlice(coin, WabiSabiFactory.CreateOwnershipProof(key));

			var initialRemaining = anotherRound.RemainingInputVsizeAllocation;
			anotherRound.Alices.Add(preAlice);

			var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
			var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
			var resp = await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, key, CancellationToken.None).ConfigureAwait(false);

			AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);
			Assert.Empty(anotherRound.Alices);
			Assert.Equal(initialRemaining, anotherRound.RemainingInputVsizeAllocation);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
