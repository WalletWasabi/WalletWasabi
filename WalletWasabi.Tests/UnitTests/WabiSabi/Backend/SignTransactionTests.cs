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
	public class SignTransactionTests
	{
		[Fact]
		public async Task SuccessAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Key key = new();
			Alice alice = WabiSabiFactory.CreateAlice(key: key);
			round.Alices.Add(alice);
			var coinjoin = Transaction.Create(Network.Main);
			coinjoin.Inputs.Add(alice.Coins.First().Outpoint);
			round.Coinjoin = coinjoin;
			round.Phase = Phase.TransactionSigning;
			arena.OnTryGetRound = _ => round;

			var signedCoinJoin = coinjoin.Clone();
			signedCoinJoin.Sign(key.GetBitcoinSecret(Network.Main), alice.Coins.First());

			var req = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair(0, signedCoinJoin.Inputs[0].WitScript) });
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			handler.SignTransaction(req);
			Assert.True(round.Coinjoin.Inputs.First().HasWitScript());
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			MockArena arena = new();
			arena.OnTryGetRound = _ => null;

			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			var req = new TransactionSignaturesRequest(Guid.NewGuid(), null!);
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.SignTransaction(req));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			arena.OnTryGetRound = _ => round;

			var req = new TransactionSignaturesRequest(round.Id, null!);
			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.TransactionSigning)
				{
					round.Phase = phase;
					await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
					var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.SignTransaction(req));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
				}
			}
		}

		[Fact]
		public async Task WrongCoinjoinSignatureAsync()
		{
			MockArena arena = new();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Key key1 = new();
			Alice alice1 = WabiSabiFactory.CreateAlice(key: key1);
			using Key key2 = new();
			Alice alice2 = WabiSabiFactory.CreateAlice(key: key2);
			round.Alices.Add(alice1);
			round.Alices.Add(alice2);
			var coinjoin = Transaction.Create(Network.Main);
			coinjoin.Inputs.Add(alice1.Coins.First().Outpoint);
			coinjoin.Inputs.Add(alice2.Coins.First().Outpoint);
			round.Coinjoin = coinjoin;
			round.Phase = Phase.TransactionSigning;
			arena.OnTryGetRound = _ => round;

			// Submit the signature for the second alice to the first alice's input.
			var signedCoinJoin = coinjoin.Clone();
			signedCoinJoin.Sign(key2.GetBitcoinSecret(Network.Main), alice2.Coins.First());

			var req = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair(0, signedCoinJoin.Inputs[0].WitScript) });
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var ex = Assert.Throws<WabiSabiProtocolException>(() => handler.SignTransaction(req));
			Assert.Equal(WabiSabiProtocolErrorCode.WrongCoinjoinSignature, ex.ErrorCode);
		}
	}
}
