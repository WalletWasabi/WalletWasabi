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
	public class RegisterOutputTests
	{
		[Fact]
		public async Task SuccessAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);
			var resp = await handler.RegisterOutputAsync(req);
			Assert.NotEmpty(round.Bobs);
			Assert.NotNull(resp);
			Assert.NotNull(resp.AmountCredentials);
			Assert.NotNull(resp.WeightCredentials);
			Assert.NotNull(resp.UnsignedTransactionSecret);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync();
			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateOutputRegistrationRequest();
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterOutputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task ScriptNotAllowedAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);
			using Key key = new();

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round, key.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ScriptPubKey);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterOutputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task NotEnoughFundsAsync()
		{
			WabiSabiConfig cfg = new() { MinRegistrableAmount = Money.Coins(2) };
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			round.Alices.Add(WabiSabiFactory.CreateAlice(value: Money.Coins(1)));
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);

			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterOutputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task TooMuchFundsAsync()
		{
			WabiSabiConfig cfg = new() { MaxRegistrableAmount = Money.Coins(1.999m) };
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			round.Alices.Add(WabiSabiFactory.CreateAlice(value: Money.Coins(2)));
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);

			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterOutputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task IncorrectRequestedWeightCredentialsAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round, weight: 30);

			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterOutputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.IncorrectRequestedWeightCredentials, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.OutputRegistration)
				{
					var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);
					round.Phase = phase;
					var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterOutputAsync(req));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
				}
			}

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
