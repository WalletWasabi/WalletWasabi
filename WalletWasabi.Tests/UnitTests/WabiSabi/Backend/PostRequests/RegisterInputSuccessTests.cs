using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests
{
	public class RegisterInputSuccessTests
	{
		private static void AssertSingleAliceSuccessfullyRegistered(Round round, DateTimeOffset minAliceDeadline, InputRegistrationResponse resp)
		{
			var alice = Assert.Single(round.Alices);
			Assert.NotNull(resp);
			Assert.NotNull(resp.AmountCredentials);
			Assert.NotNull(resp.VsizeCredentials);
			Assert.True(minAliceDeadline <= alice.Deadline);
		}

		[Fact]
		public async Task SuccessAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			using Key key = new();

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));

			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);
			var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
			var resp = await handler.RegisterInputAsync(req);
			AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task SuccessWithAliceUpdateIntraRoundAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			using Key key = new();

			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);

			// Make sure an Alice have already been registered with the same input.
			var preAlice = WabiSabiFactory.CreateAlice(prevout: req.Input, roundSignature: req.RoundSignature);
			round.Alices.Add(preAlice);

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
			var resp = await handler.RegisterInputAsync(req);
			AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task SuccessWithAliceUpdateCrossRoundAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var anotherRound = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round, anotherRound);
			using Key key = new();

			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);

			// Make sure an Alice have already been registered with the same input.
			var preAlice = WabiSabiFactory.CreateAlice(prevout: req.Input, roundSignature: req.RoundSignature);

			var initialRemaining = anotherRound.RemainingInputVsizeAllocation;
			anotherRound.Alices.Add(preAlice);

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
			var resp = await handler.RegisterInputAsync(req);
			AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);
			Assert.Empty(anotherRound.Alices);
			Assert.Equal(initialRemaining, anotherRound.RemainingInputVsizeAllocation);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
