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
using WalletWasabi.WabiSabi.Backend.Models;
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
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);
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
					var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
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
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, ex.ErrorCode);
		}

		[Fact]
		public async Task InputUnconfirmedAsync()
		{
			MockArena arena = new();
			Round round = new();
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();
			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { Confirmations = 0 };

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, rpc);
			var req = new InputsRegistrationRequest(
				round.Id,
				WabiSabiFactory.CreateInputRoundSignaturePairs(1),
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputUnconfirmed, ex.ErrorCode);
		}

		[Fact]
		public async Task InputImmatureAsync()
		{
			MockArena arena = new();
			Round round = new();
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();

			for (int i = 1; i <= 100; i++)
			{
				rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { Confirmations = i, IsCoinBase = true };

				await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, rpc);
				var req = new InputsRegistrationRequest(
					round.Id,
					WabiSabiFactory.CreateInputRoundSignaturePairs(1),
					null!,
					null!);
				var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
				Assert.Equal(WabiSabiProtocolErrorCode.InputImmature, ex.ErrorCode);
			}
		}

		[Fact]
		public async Task InputScriptNotAllowedAsync()
		{
			MockArena arena = new();
			Round round = new();
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { Confirmations = 1, ScriptPubKeyType = "foo" };

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, rpc);
			var req = new InputsRegistrationRequest(
				round.Id,
				WabiSabiFactory.CreateInputRoundSignaturePairs(1),
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputScriptNotAllowed, ex.ErrorCode);
		}
	}
}
