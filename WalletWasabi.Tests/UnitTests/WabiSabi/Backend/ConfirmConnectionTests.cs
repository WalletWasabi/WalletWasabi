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
		public async Task RoundNotFoundAsync()
		{
			MockArena arena = new();
			arena.OnTryGetRound = _ => null;

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			var req = new ConnectionConfirmationRequest(Guid.NewGuid(), Guid.NewGuid(), null!, null!, null!, null!);
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.ConfirmConnection(req));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;

			var req = new ConnectionConfirmationRequest(round.Id, Guid.NewGuid(), null!, null!, null!, null!);
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
		}

		[Fact]
		public async Task AliceNotFoundAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;

			var req = new ConnectionConfirmationRequest(round.Id, Guid.NewGuid(), null!, null!, null!, null!);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.ConfirmConnection(req));
			Assert.Equal(WabiSabiProtocolErrorCode.AliceNotFound, ex.ErrorCode);
		}
	}
}
