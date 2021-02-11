using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class InputRegistrationTests
	{
		[Fact]
		public async Task RoundNotFoundAsync()
		{
			MockArena arena = new();
			arena.OnTryGetRound = _ => null;

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			var req = new InputsRegistrationRequest(
				Guid.NewGuid(),
				WabiSabiFactory.CreateInputRoundSignaturePairs(1),
				null!,
				null!);
			await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await handler.RegisterInputAsync(req));
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			MockArena arena = new();
			Round round = new();
			arena.OnTryGetRound = _ => round;

			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.InputRegistration)
				{
					round.Phase = phase;
					await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
					var req = new InputsRegistrationRequest(
						round.Id,
						WabiSabiFactory.CreateInputRoundSignaturePairs(1),
						null!,
						null!);
					await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await handler.RegisterInputAsync(req));
				}
			}
		}

		[Fact]
		public async Task InputSpentAsync()
		{
			MockArena arena = new();
			Round round = new();
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();
			rpc.OnGetTxOutAsync = (_, _, _) => null;

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, rpc);
			var req = new InputsRegistrationRequest(
				round.Id,
				WabiSabiFactory.CreateInputRoundSignaturePairs(1),
				null!,
				null!);
			await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await handler.RegisterInputAsync(req));
		}
	}
}
