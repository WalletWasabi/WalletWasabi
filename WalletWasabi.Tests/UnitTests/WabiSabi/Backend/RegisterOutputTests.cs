using NBitcoin;
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
	public class RegisterOutputTests
	{
		[Fact]
		public async Task SuccessAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			arena.OnTryGetRound = _ => round;
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);
			var resp = handler.RegisterOutput(req);
			Assert.NotEmpty(round.Bobs);
			Assert.NotNull(resp);
			Assert.NotNull(resp.AmountCredentials);
			Assert.NotNull(resp.WeightCredentials);
			Assert.NotNull(resp.UnsignedTransactionSecret);
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			MockArena arena = new();
			arena.OnTryGetRound = _ => null;

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			var req = WabiSabiFactory.CreateOutputRegistrationRequest();
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.RegisterOutput(req));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);
		}

		[Fact]
		public async Task ScriptNotAllowedAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			arena.OnTryGetRound = _ => round;
			using Key key = new();

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round, key.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ScriptPubKey);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.RegisterOutput(req));
			Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);
		}

		[Fact]
		public async Task NotEnoughFundsAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new() { MinRegistrableAmount = Money.Coins(2) };
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			round.Alices.Add(WabiSabiFactory.CreateAlice(value: Money.Coins(1)));
			arena.OnTryGetRound = _ => round;
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);

			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.RegisterOutput(req));
			Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, ex.ErrorCode);
		}

		[Fact]
		public async Task TooMuchFundsAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new() { MaxRegistrableAmount = Money.Coins(1.999m) };
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			round.Alices.Add(WabiSabiFactory.CreateAlice(value: Money.Coins(2)));
			arena.OnTryGetRound = _ => round;
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);

			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.RegisterOutput(req));
			Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, ex.ErrorCode);
		}

		[Fact]
		public async Task IncorrectRequestedWeightCredentialsAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.OutputRegistration;
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			arena.OnTryGetRound = _ => round;
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			var req = WabiSabiFactory.CreateOutputRegistrationRequest(round, weight: 30);

			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.RegisterOutput(req));
			Assert.Equal(WabiSabiProtocolErrorCode.IncorrectRequestedWeightCredentials, ex.ErrorCode);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			arena.OnTryGetRound = _ => round;
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.OutputRegistration)
				{
					var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);
					round.Phase = phase;
					var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.RegisterOutput(req));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
				}
			}
		}
	}
}
