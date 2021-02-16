using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var alice = WabiSabiFactory.CreateAlice();
			var preDeadline = alice.Deadline;
			round.Alices.Add(alice);
			arena.OnTryGetRound = _ => round;

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
		}

		[Fact]
		public async Task SuccessInConnectionConfirmationPhaseAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.ConnectionConfirmation;
			var alice = WabiSabiFactory.CreateAlice();
			var preDeadline = alice.Deadline;
			round.Alices.Add(alice);
			arena.OnTryGetRound = _ => round;

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var resp = handler.ConfirmConnection(req);
			Assert.NotNull(resp);
			Assert.NotNull(resp.ZeroAmountCredentials);
			Assert.NotNull(resp.ZeroWeightCredentials);
			Assert.NotNull(resp.RealAmountCredentials);
			Assert.NotNull(resp.RealWeightCredentials);
			Assert.Equal(preDeadline, alice.Deadline);
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			MockArena arena = new();
			arena.OnTryGetRound = _ => null;

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateConnectionConfirmationRequest();
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.ConfirmConnection(req));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var alice = WabiSabiFactory.CreateAlice();
			var preDeadline = alice.Deadline;
			round.Alices.Add(alice);
			arena.OnTryGetRound = _ => round;

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
		}

		[Fact]
		public async Task AliceNotFoundAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.ConfirmConnection(req));
			Assert.Equal(WabiSabiProtocolErrorCode.AliceNotFound, ex.ErrorCode);
		}
	}
}
