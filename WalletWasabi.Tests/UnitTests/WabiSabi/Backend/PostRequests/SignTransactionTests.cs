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

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests
{
	public class SignTransactionTests
	{
		[Fact]
		public async Task SuccessAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Key key = new();
			Alice alice = WabiSabiFactory.CreateAlice(key: key);
			round.Alices.Add(alice);
			var coinjoin = round.Coinjoin;
			coinjoin.Inputs.Add(alice.Coins.First().Outpoint);
			round.SetPhase(Phase.TransactionSigning);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			var signedCoinJoin = coinjoin.Clone();
			signedCoinJoin.Sign(key.GetBitcoinSecret(Network.Main), alice.Coins.First());

			var req = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair(0, signedCoinJoin.Inputs[0].WitScript) });
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			await handler.SignTransactionAsync(req);
			Assert.True(round.Coinjoin.Inputs.First().HasWitScript());
			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync();
			await using PostRequestHandler handler = new(new WabiSabiConfig(), new Prison(), arena, new MockRpcClient());
			var req = new TransactionSignaturesRequest(Guid.NewGuid(), null!);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.SignTransactionAsync(req));
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

			var req = new TransactionSignaturesRequest(round.Id, null!);
			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.TransactionSigning)
				{
					round.SetPhase(phase);
					await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
					var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.SignTransactionAsync(req));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
				}
			}
			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task WrongCoinjoinSignatureAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Key key1 = new();
			Alice alice1 = WabiSabiFactory.CreateAlice(key: key1);
			using Key key2 = new();
			Alice alice2 = WabiSabiFactory.CreateAlice(key: key2);
			round.Alices.Add(alice1);
			round.Alices.Add(alice2);
			var coinjoin = round.Coinjoin;
			coinjoin.Inputs.Add(alice1.Coins.First().Outpoint);
			coinjoin.Inputs.Add(alice2.Coins.First().Outpoint);
			round.SetPhase(Phase.TransactionSigning);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			// Submit the signature for the second alice to the first alice's input.
			var signedCoinJoin = coinjoin.Clone();
			signedCoinJoin.Sign(key2.GetBitcoinSecret(Network.Main), alice2.Coins.First());

			var req = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair(0, signedCoinJoin.Inputs[0].WitScript) });
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.SignTransactionAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.WrongCoinjoinSignature, ex.ErrorCode);
			await arena.StopAsync(CancellationToken.None);
		}
	}
}
