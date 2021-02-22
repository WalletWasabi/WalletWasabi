using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class ConfirmConnectionTests
	{
		[Fact]
		public async Task SuccessInInputRegistrationPhaseAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var alice = WabiSabiFactory.CreateAlice();
			var preDeadline = alice.Deadline;
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var resp = handler.ConfirmConnection(req);
			Assert.NotNull(resp);
			Assert.NotNull(resp.ZeroAmountCredentials);
			Assert.NotNull(resp.ZeroWeightCredentials);
			Assert.Null(resp.RealAmountCredentials);
			Assert.Null(resp.RealWeightCredentials);
			Assert.NotEqual(preDeadline, alice.Deadline);
			Assert.True(minAliceDeadline <= alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task SuccessInConnectionConfirmationPhaseAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.ConnectionConfirmation;
			var alice = WabiSabiFactory.CreateAlice();
			var preDeadline = alice.Deadline;
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var resp = handler.ConfirmConnection(req);
			Assert.NotNull(resp);
			Assert.NotNull(resp.ZeroAmountCredentials);
			Assert.NotNull(resp.ZeroWeightCredentials);
			Assert.NotNull(resp.RealAmountCredentials);
			Assert.NotNull(resp.RealWeightCredentials);
			Assert.Equal(preDeadline, alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync();
			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateConnectionConfirmationRequest();
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.ConfirmConnection(req));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var alice = WabiSabiFactory.CreateAlice();
			var preDeadline = alice.Deadline;
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.InputRegistration && phase != Phase.ConnectionConfirmation)
				{
					round.Phase = phase;
					await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
					var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.ConfirmConnection(req));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
				}
			}
			Assert.Equal(preDeadline, alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AliceNotFoundAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.ConfirmConnection(req));
			Assert.Equal(WabiSabiProtocolErrorCode.AliceNotFound, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task IncorrectRequestedWeightCredentialsAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.ConnectionConfirmation;
			var alice = WabiSabiFactory.CreateAlice();
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			req = new(
				req.RoundId,
				req.AliceId,
				req.ZeroAmountCredentialRequests,
				req.RealAmountCredentialRequests,
				req.ZeroWeightCredentialRequests,
				WabiSabiFactory.CreateRealCredentialRequests(round, null, 234).weightReq);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.ConfirmConnection(req));
			Assert.Equal(WabiSabiProtocolErrorCode.IncorrectRequestedWeightCredentials, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task IncorrectRequestedAmountCredentialsAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.ConnectionConfirmation;
			var alice = WabiSabiFactory.CreateAlice();
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			req = new(
				req.RoundId,
				req.AliceId,
				req.ZeroAmountCredentialRequests,
				WabiSabiFactory.CreateRealCredentialRequests(round, Money.Coins(3), null).amountReq,
				req.ZeroWeightCredentialRequests,
				req.RealWeightCredentialRequests);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.ConfirmConnection(req));
			Assert.Equal(WabiSabiProtocolErrorCode.IncorrectRequestedAmountCredentials, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
