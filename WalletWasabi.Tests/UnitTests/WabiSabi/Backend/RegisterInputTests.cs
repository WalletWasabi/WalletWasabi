using NBitcoin;
using NBitcoin.RPC;
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
	public class RegisterInputTests
	{
		[Fact]
		public async Task SuccessAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);

			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			var req = WabiSabiFactory.CreateInputsRegistrationRequest(key, round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
			var resp = await handler.RegisterInputAsync(req);
			var alice = Assert.Single(round.Alices);
			Assert.NotNull(resp);
			Assert.NotNull(resp.AmountCredentials);
			Assert.NotNull(resp.WeightCredentials);
			Assert.True(minAliceDeadline <= alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest();
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			var req = WabiSabiFactory.CreateInputsRegistrationRequest(key, round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.InputRegistration)
				{
					round.Phase = phase;
					var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
				}
			}

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task NonUniqueInputsAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			var inputSigPair = WabiSabiFactory.CreateInputRoundSignaturePair();

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(new[] { inputSigPair, inputSigPair }, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.NonUniqueInputs, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task TooManyInputsAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new() { MaxInputCountByAlice = 3 };
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);

			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(WabiSabiFactory.CreateInputRoundSignaturePairs(4), round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.TooManyInputs, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputBannedAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			Prison prison = new();
			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			prison.Punish(pair.Input, Punishment.Banned, Guid.NewGuid());
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);

			await using PostRequestHandler handler = new(cfg, prison, arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(pair, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputCanBeNotedAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			Prison prison = new();
			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			prison.Punish(pair.Input, Punishment.Noted, Guid.NewGuid());
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);

			await using PostRequestHandler handler = new(cfg, prison, arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(pair, round);
			var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await handler.RegisterInputAsync(req));
			if (ex is WabiSabiProtocolException wspex)
			{
				Assert.NotEqual(WabiSabiProtocolErrorCode.InputBanned, wspex.ErrorCode);
			}

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputCantBeNotedAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			Prison prison = new();
			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			prison.Punish(pair.Input, Punishment.Noted, Guid.NewGuid());
			WabiSabiConfig cfg = new() { AllowNotedInputRegistration = false };
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);

			await using PostRequestHandler handler = new(cfg, prison, arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(pair, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputNotWhitelistedAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			Round blameRound = new(round);

			arena.Rounds.Add(round.Id, round);
			arena.Rounds.Add(blameRound.Id, blameRound);

			MockRpcClient rpc = new();
			using Key key = new();
			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(blameRound);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputNotWhitelisted, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputWhitelistedAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice(pair));
			Round blameRound = new(round);

			arena.Rounds.Add(round.Id, round);
			arena.Rounds.Add(blameRound.Id, blameRound);

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(pair, blameRound);

			var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await handler.RegisterInputAsync(req));
			if (ex is WabiSabiProtocolException wspex)
			{
				Assert.NotEqual(WabiSabiProtocolErrorCode.InputNotWhitelisted, wspex.ErrorCode);
			}

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputWhitelistedButBannedAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			Prison prison = new();
			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			prison.Punish(pair.Input, Punishment.Banned, Guid.NewGuid());
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice(pair));
			Round blameRound = new(round);

			arena.Rounds.Add(round.Id, round);
			arena.Rounds.Add(blameRound.Id, blameRound);

			await using PostRequestHandler handler = new(cfg, prison, arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(pair, blameRound);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputSpentAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			MockRpcClient rpc = new();
			rpc.OnGetTxOutAsync = (_, _, _) => null;

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputUnconfirmedAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			MockRpcClient rpc = new();
			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { Confirmations = 0 };

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputUnconfirmed, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputImmatureAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			MockRpcClient rpc = new();

			var req = WabiSabiFactory.CreateInputsRegistrationRequest(round);
			for (int i = 1; i <= 100; i++)
			{
				rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { Confirmations = i, IsCoinBase = true };

				await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
				var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
				Assert.Equal(WabiSabiProtocolErrorCode.InputImmature, ex.ErrorCode);
			}

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task ScriptNotAllowedAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse
			{
				Confirmations = 1,
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetScriptAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task WrongRoundSignatureAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.WrongRoundSignature, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task NotEnoughFundsAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new() { MinRegistrableAmount = Money.Coins(2) };
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(key, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task TooMuchFundsAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new() { MaxRegistrableAmount = Money.Coins(0.9m) };
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(key, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task TooMuchWeightAsync()
		{
			using Arena arena = new(TimeSpan.FromSeconds(1));
			await arena.StartAsync(CancellationToken.None);

			WabiSabiConfig cfg = new() { RegistrableWeightCredentials = 1 };
			var round = WabiSabiFactory.CreateRound(cfg);

			arena.Rounds.Add(round.Id, round);
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(key, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchWeight, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
