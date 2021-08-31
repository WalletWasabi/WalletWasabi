using NBitcoin;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class AliceTimeoutTests
	{
		[Fact]
		public async Task AliceTimesoutAsync()
		{
			// Alice times out when its deadline is reached.
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var km = ServiceFactory.CreateKeyManager("");
			var key = BitcoinFactory.CreateHdPubKey(km);
			var smartCoin = BitcoinFactory.CreateSmartCoin(key, 10m);
			var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(smartCoin.Coin);

			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, rpc, round);
			var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

			using RoundStateUpdater roundStateUpdater = new(TimeSpan.FromSeconds(2), new ArenaRequestHandlerAdapter(arena));
			await roundStateUpdater.StartAsync(CancellationToken.None);

			// Register Alices.
			var esk = km.GetSecrets("", smartCoin.ScriptPubKey).Single();
			var aliceClient = new AliceClient(RoundState.FromRound(round), arenaClient, smartCoin, esk.PrivateKey.GetBitcoinSecret(round.Network));

			using CancellationTokenSource cancellationTokenSource = new();
			var task = aliceClient.RegisterAndConfirmInputAsync(roundStateUpdater, cancellationTokenSource.Token);

			while (round.Alices.Count == 0)
			{
				await Task.Delay(10);
			}

			var alice = Assert.Single(round.Alices);
			alice.Deadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

			Assert.Empty(round.Alices);

			cancellationTokenSource.Cancel();
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);

			await roundStateUpdater.StopAsync(CancellationToken.None);
			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AliceDoesntTimeoutInConnectionConfirmationAsync()
		{
			// Alice does not time out when it's not input registration anymore,
			// even though the deadline is reached.
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.SetPhase(Phase.ConnectionConfirmation);
			var alice = WabiSabiFactory.CreateAlice(round);
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using ArenaRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			Assert.Single(round.Alices);
			DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
			alice.Deadline = preDeadline;
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Single(round.Alices);
			Assert.Equal(preDeadline, alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AliceDoesntTimeoutIfInputRegistrationTimedoutAsync()
		{
			// Alice does not time out if input registration timed out,
			// even though the deadline is reached and still in input reg.
			WabiSabiConfig cfg = new() { StandardInputRegistrationTimeout = TimeSpan.Zero };
			var round = WabiSabiFactory.CreateRound(cfg);
			var alice = WabiSabiFactory.CreateAlice(round);
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using ArenaRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			Assert.Single(round.Alices);
			DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
			alice.Deadline = preDeadline;
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Single(round.Alices);
			Assert.Equal(preDeadline, alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AliceDoesntTimeoutIfMaxInputCountReachedAsync()
		{
			// Alice does not time out if input reg is full with alices,
			// even though the deadline is reached and still in input reg.
			WabiSabiConfig cfg = new() { MaxInputCountByRound = 3 };
			var round = WabiSabiFactory.CreateRound(cfg);
			var alice = WabiSabiFactory.CreateAlice(round);
			round.Alices.Add(alice);
			round.Alices.Add(WabiSabiFactory.CreateAlice(round));
			round.Alices.Add(WabiSabiFactory.CreateAlice(round));
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using ArenaRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			Assert.Equal(3, round.Alices.Count);
			DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
			alice.Deadline = preDeadline;
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(3, round.Alices.Count);
			Assert.Equal(preDeadline, alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AliceDeadlineUpdatedAsync()
		{
			// Alice's deadline is updated by connection confirmation.
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var alice = WabiSabiFactory.CreateAlice(round);
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using ArenaRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			Assert.Single(round.Alices);
			DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
			alice.Deadline = preDeadline;
			await handler.ConfirmConnectionAsync(req, CancellationToken.None);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Single(round.Alices);
			Assert.NotEqual(preDeadline, alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
