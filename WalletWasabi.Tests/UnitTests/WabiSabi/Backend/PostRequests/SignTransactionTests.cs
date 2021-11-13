using NBitcoin;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
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
			Alice alice = WabiSabiFactory.CreateAlice(key: key, round: round);
			round.Alices.Add(alice);
			round.CoinjoinState = round.AddInput(alice.Coin).Finalize();
			round.SetPhase(Phase.TransactionSigning);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round);

			var aliceSignedCoinJoin = round.Assert<SigningState>().CreateUnsignedTransaction();
			aliceSignedCoinJoin.Sign(key.GetBitcoinSecret(Network.Main), alice.Coin);

			var req = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair(0, aliceSignedCoinJoin.Inputs[0].WitScript) });
			await arena.SignTransactionAsync(req, CancellationToken.None);
			Assert.True(round.Assert<SigningState>().IsFullySigned);
			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task RoundNotFoundAsync()
		{
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync();
			var req = new TransactionSignaturesRequest(uint256.Zero, null!);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.SignTransactionAsync(req, CancellationToken.None));
			Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);
			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task WrongPhaseAsync()
		{
			WabiSabiConfig cfg = new();
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = arena.Rounds.First();

			var req = new TransactionSignaturesRequest(round.Id, null!);
			foreach (Phase phase in Enum.GetValues(typeof(Phase)))
			{
				if (phase != Phase.TransactionSigning)
				{
					round.SetPhase(phase);
	
					var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () =>
						await arena.SignTransactionAsync(req, CancellationToken.None));
					Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
				}
			}
			await arena.StopAsync(CancellationToken.None);
		}
	}
}
