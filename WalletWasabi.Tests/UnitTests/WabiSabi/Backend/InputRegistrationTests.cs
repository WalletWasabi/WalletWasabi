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
		public async Task InputbannedAsync()
		{
			MockArena arena = new();
			Prison prison = new();
			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			prison.Punish(pair.Input, Punishment.Banned, Guid.NewGuid());

			await using PostRequestHandler handler = new(new WabiSabiConfig(), prison, arena, new MockRpcClient());
			var req = new InputsRegistrationRequest(
				Guid.NewGuid(),
				new[] { pair },
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);
		}

		[Fact]
		public async Task InputCanBeNotedAsync()
		{
			MockArena arena = new();
			Prison prison = new();
			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			prison.Punish(pair.Input, Punishment.Noted, Guid.NewGuid());

			await using PostRequestHandler handler = new(new WabiSabiConfig(), prison, arena, new MockRpcClient());
			var req = new InputsRegistrationRequest(
				Guid.NewGuid(),
				new[] { pair },
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.NotEqual(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);
		}

		[Fact]
		public async Task InputCantBeNotedAsync()
		{
			MockArena arena = new();
			Prison prison = new();
			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			prison.Punish(pair.Input, Punishment.Noted, Guid.NewGuid());

			await using PostRequestHandler handler = new(new WabiSabiConfig() { AllowNotedInputRegistration = false }, prison, arena, new MockRpcClient());
			var req = new InputsRegistrationRequest(
				Guid.NewGuid(),
				new[] { pair },
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);
		}

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
			var cfg = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;

			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.InputRegistration)
				{
					round.Phase = phase;
					await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
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
		public async Task NonUniqueInputsAsync()
		{
			MockArena arena = new();
			var cfg = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			var inputSigPair = WabiSabiFactory.CreateInputRoundSignaturePair();

			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.InputRegistration)
				{
					round.Phase = phase;
					await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
					var req = new InputsRegistrationRequest(
						round.Id,
						new[] { inputSigPair, inputSigPair },
						null!,
						null!);
					var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
				}
			}
		}

		[Fact]
		public async Task TooManyInputsAsync()
		{
			MockArena arena = new();
			var cfg = new WabiSabiConfig() { MaxInputCountByAlice = 3 };
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;

			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.InputRegistration)
				{
					round.Phase = phase;
					await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
					var req = new InputsRegistrationRequest(
						round.Id,
						WabiSabiFactory.CreateInputRoundSignaturePairs(4),
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
			var cfg = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();
			rpc.OnGetTxOutAsync = (_, _, _) => null;

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
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
			var cfg = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();
			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { Confirmations = 0 };

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
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
			var cfg = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();

			for (int i = 1; i <= 100; i++)
			{
				rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { Confirmations = i, IsCoinBase = true };

				await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
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
			var cfg = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { Confirmations = 1, ScriptPubKeyType = "foo" };

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = new InputsRegistrationRequest(
				round.Id,
				WabiSabiFactory.CreateInputRoundSignaturePairs(1),
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputScriptNotAllowed, ex.ErrorCode);
		}

		[Fact]
		public async Task WrongRoundSignatureAsync()
		{
			MockArena arena = new();
			var cfg = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = new InputsRegistrationRequest(
				round.Id,
				WabiSabiFactory.CreateInputRoundSignaturePairs(1),
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.WrongRoundSignature, ex.ErrorCode);
		}

		[Fact]
		public async Task NotEnoughFundsAsync()
		{
			MockArena arena = new();
			var cfg = new WabiSabiConfig() { MinRegistrableAmount = Money.Coins(2) };
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = new InputsRegistrationRequest(
				round.Id,
				WabiSabiFactory.CreateInputRoundSignaturePairs(new[] { key }, round.Hash),
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, ex.ErrorCode);
		}

		[Fact]
		public async Task TooMuchFundsAsync()
		{
			MockArena arena = new();
			var cfg = new WabiSabiConfig() { MaxRegistrableAmount = Money.Coins(0.9m) };
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = new InputsRegistrationRequest(
				round.Id,
				WabiSabiFactory.CreateInputRoundSignaturePairs(new[] { key }, round.Hash),
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, ex.ErrorCode);
		}

		[Fact]
		public async Task NotEnoughWeightAsync()
		{
			MockArena arena = new();
			var cfg = new WabiSabiConfig() { MinRegistrableWeight = 1000 };
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = new InputsRegistrationRequest(
				round.Id,
				WabiSabiFactory.CreateInputRoundSignaturePairs(new[] { key }, round.Hash),
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughWeight, ex.ErrorCode);
		}

		[Fact]
		public async Task TooMuchWeightAsync()
		{
			MockArena arena = new();
			var cfg = new WabiSabiConfig() { MaxRegistrableWeight = 1 };
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = new InputsRegistrationRequest(
				round.Id,
				WabiSabiFactory.CreateInputRoundSignaturePairs(new[] { key }, round.Hash),
				null!,
				null!);
			var ex = await Assert.ThrowsAnyAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchWeight, ex.ErrorCode);
		}
	}
}
