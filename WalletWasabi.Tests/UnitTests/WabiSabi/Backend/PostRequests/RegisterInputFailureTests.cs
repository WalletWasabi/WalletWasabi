using NBitcoin;
using System;
using System.Linq;
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

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests
{
	public class RegisterInputFailureTests
	{
		private static async Task RegisterAndAssertWrongPhaseAsync(InputRegistrationRequest req, ArenaRequestHandler handler)
		{
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync();
			using Key key = new();

			await using ArenaRequestHandler handler = new(new(), new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var req = WabiSabiFactory.CreateInputRegistrationRequest();
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			WabiSabiConfig cfg = new();
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21)).ConfigureAwait(false);
			var round = arena.Rounds.First().Value;
			using Key key = new();
			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);
			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));

			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.InputRegistration)
				{
					round.SetPhase(phase);
					await RegisterAndAssertWrongPhaseAsync(req, handler);
				}
			}

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputRegistrationFullAsync()
		{
			WabiSabiConfig cfg = new() { MaxInputCountByRound = 3 };
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			using Key key = new();

			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);
			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			await RegisterAndAssertWrongPhaseAsync(req, handler);
			Assert.Equal(Phase.InputRegistration, round.Phase);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputRegistrationFullVsizeAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			using Key key = new();

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));

			// TODO add configuration parameter
			round.InitialInputVsizeAllocation = (int)round.PerAliceVsizeAllocation;

			// Add an alice
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			round.Alices.Add(WabiSabiFactory.CreateAlice());

			Assert.Equal(0, round.RemainingInputVsizeAllocation);

			// try to add an additional input
			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.VsizeQuotaExceeded, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputRegistrationTimedoutAsync()
		{
			WabiSabiConfig cfg = new() { StandardInputRegistrationTimeout = TimeSpan.Zero };
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg);
			using Key key = new();

			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);
			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

			arena.Rounds.Add(round.Id, round);
			await RegisterAndAssertWrongPhaseAsync(req, handler);
			Assert.Equal(Phase.InputRegistration, round.Phase);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputRegistrationTimeoutCanBeModifiedRuntimeAsync()
		{
			WabiSabiConfig cfg = new() { StandardInputRegistrationTimeout = TimeSpan.FromHours(1) };

			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg);
			using Key key = new();

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = arena.Rounds.First().Value;
			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);

			cfg.StandardInputRegistrationTimeout = TimeSpan.Zero;
			await RegisterAndAssertWrongPhaseAsync(req, handler);
			Assert.Equal(Phase.InputRegistration, round.Phase);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputBannedAsync()
		{
			Prison prison = new();
			var key = new Key();
			var outpoint = BitcoinFactory.CreateOutPoint();
			prison.Punish(outpoint, Punishment.Banned, Guid.NewGuid());
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			await using ArenaRequestHandler handler = new(cfg, prison, arena, WabiSabiFactory.CreateMockRpc());
			var req = WabiSabiFactory.CreateInputRegistrationRequest(prevout: outpoint, round: round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputCanBeNotedAsync()
		{
			Prison prison = new();
			var outpoint = BitcoinFactory.CreateOutPoint();
			prison.Punish(outpoint, Punishment.Noted, Guid.NewGuid());
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			await using ArenaRequestHandler handler = new(cfg, prison, arena, WabiSabiFactory.CreateMockRpc());
			var req = WabiSabiFactory.CreateInputRegistrationRequest(prevout: outpoint, round: round);
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
			Prison prison = new();
			var outpoint = BitcoinFactory.CreateOutPoint();
			prison.Punish(outpoint, Punishment.Noted, Guid.NewGuid());
			WabiSabiConfig cfg = new() { AllowNotedInputRegistration = false };
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			await using ArenaRequestHandler handler = new(cfg, prison, arena, WabiSabiFactory.CreateMockRpc());
			var req = WabiSabiFactory.CreateInputRegistrationRequest(prevout: outpoint, round: round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputSpentAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			MockRpcClient rpc = new();
			rpc.OnGetTxOutAsync = (_, _, _) => null;

			await using ArenaRequestHandler handler = new(cfg, new(), arena, rpc);
			var req = WabiSabiFactory.CreateInputRegistrationRequest(round: round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputSpent, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputUnconfirmedAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			MockRpcClient rpc = new();
			rpc.OnGetTxOutAsync = (_, _, _) => new() { Confirmations = 0 };

			await using ArenaRequestHandler handler = new(cfg, new(), arena, rpc);
			var req = WabiSabiFactory.CreateInputRegistrationRequest(round: round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputUnconfirmed, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputImmatureAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			MockRpcClient rpc = new();

			var req = WabiSabiFactory.CreateInputRegistrationRequest(round: round);
			for (int i = 1; i <= 100; i++)
			{
				rpc.OnGetTxOutAsync = (_, _, _) => new() { Confirmations = i, IsCoinBase = true };

				await using ArenaRequestHandler handler = new(cfg, new(), arena, rpc);
				var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
				Assert.Equal(WabiSabiProtocolErrorCode.InputImmature, ex.ErrorCode);
			}

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task ScriptNotAllowedAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				TxOut = new(Money.Coins(1), key.PubKey.GetScriptAddress(Network.Main))
			};

			await using ArenaRequestHandler handler = new(cfg, new(), arena, rpc);
			var req = WabiSabiFactory.CreateInputRegistrationRequest(round: round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task WrongRoundSignatureAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			using Key key = new();

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var req = WabiSabiFactory.CreateInputRegistrationRequest(round: round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.WrongRoundSignature, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task NotEnoughFundsAsync()
		{
			WabiSabiConfig cfg = new() { MinRegistrableAmount = Money.Coins(2) };
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			using Key key = new();

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task TooMuchFundsAsync()
		{
			WabiSabiConfig cfg = new() { MaxRegistrableAmount = Money.Coins(0.9m) };
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			using Key key = new();

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task TooMuchVsizeAsync()
		{
			WabiSabiConfig cfg = new() { PerAliceVsizeAllocation = 1 };
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			using Key key = new();

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchVsize, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AliceAlreadyRegisteredIntraRoundAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);
			using Key key = new();

			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);

			// Make sure an Alice have already been registered with the same input.
			var anotherAlice = WabiSabiFactory.CreateAlice(prevout: req.Input, roundSignature: req.RoundSignature);
			round.Alices.Add(anotherAlice);
			round.SetPhase(Phase.ConnectionConfirmation);

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			await RegisterAndAssertWrongPhaseAsync(req, handler);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AliceAlreadyRegisteredCrossRoundAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var anotherRound = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round, anotherRound);
			using Key key = new();

			var req = WabiSabiFactory.CreateInputRegistrationRequest(key, round);

			// Make sure an Alice have already been registered with the same input.
			var preAlice = WabiSabiFactory.CreateAlice(prevout: req.Input, roundSignature: req.RoundSignature);
			anotherRound.Alices.Add(preAlice);
			anotherRound.SetPhase(Phase.ConnectionConfirmation);

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.AliceAlreadyRegistered, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
