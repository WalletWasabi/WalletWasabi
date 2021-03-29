using Moq;
using NBitcoin;
using System;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class ArenaClientTests
	{
		[Fact]
		public async Task RegisterInputAsyncTest()
		{
			var config = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(config);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);

			using var key = new Key();
			var outpoint = BitcoinFactory.CreateOutPoint();

			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetTxOutAsync(outpoint.Hash, (int)outpoint.N, true))
				.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
				{
					IsCoinBase = false,
					Confirmations = 200,
					TxOut = new TxOut(Money.Coins(1m), key.PubKey.WitHash.GetAddress(Network.Main)),
				});
			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);

			var rnd = new InsecureRandom();
			var protocolCredentialNumber = 2;
			var protocolMaxWeightPerAlice = 1_000L;
			var amountClient = new WabiSabiClient(round.AmountCredentialIssuerParameters, protocolCredentialNumber, rnd, 4_300_000_000_000ul);
			var weightClient = new WabiSabiClient(round.WeightCredentialIssuerParameters, protocolCredentialNumber, rnd, (ulong)protocolMaxWeightPerAlice);

			var apiClient = new ArenaClient(amountClient, weightClient, coordinator);

			var aliceId = await apiClient.RegisterInputAsync(Money.Coins(1m), outpoint, key, round.Id, round.Hash);

			Assert.NotEqual(Guid.Empty, aliceId);
			Assert.Empty(apiClient.AmountCredentialClient.Credentials.Valuable);

			var reissuanceAmounts = new[]
			{
				Money.Coins(.75m) - round.FeeRate.GetFee(Constants.P2wpkhInputVirtualSize),
				Money.Coins(.25m)
			};

			var inputWeight = 4 * Constants.P2wpkhInputVirtualSize;
			var inputRemainingWeights = new[] { protocolMaxWeightPerAlice - inputWeight };

			// Phase: Input Registration
			await apiClient.ConfirmConnectionAsync(
				round.Id,
				aliceId,
				inputRemainingWeights,
				apiClient.AmountCredentialClient.Credentials.ZeroValue.Take(protocolCredentialNumber),
				reissuanceAmounts);

			Assert.Empty(apiClient.AmountCredentialClient.Credentials.Valuable);

			// Phase: Connection Confirmation
			round.SetPhase(Phase.ConnectionConfirmation);
			await apiClient.ConfirmConnectionAsync(
				round.Id,
				aliceId,
				inputRemainingWeights,
				apiClient.AmountCredentialClient.Credentials.ZeroValue.Take(protocolCredentialNumber),
				reissuanceAmounts);

			Assert.Single(apiClient.AmountCredentialClient.Credentials.Valuable, x => x.Amount.ToMoney() == reissuanceAmounts.First());
			Assert.Single(apiClient.AmountCredentialClient.Credentials.Valuable, x => x.Amount.ToMoney() == reissuanceAmounts.Last());
		}

		[Fact]
		public async Task SignTransactionAsync()
		{
			WabiSabiConfig config = new();
			Round round = WabiSabiFactory.CreateRound(config);

			using Key key1 = new();
			Alice alice1 = WabiSabiFactory.CreateAlice(key: key1);
			round.Alices.Add(alice1);

			using Key key2 = new();
			Alice alice2 = WabiSabiFactory.CreateAlice(key: key2);
			round.Alices.Add(alice2);

			var coinjoin = round.Coinjoin;
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);

			var mockRpc = new Mock<IRPCClient>();
			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);

			var rnd = new InsecureRandom();
			var amountClient = new WabiSabiClient(round.AmountCredentialIssuerParameters, 2, rnd, 4300000000000ul);
			var weightClient = new WabiSabiClient(round.WeightCredentialIssuerParameters, 2, rnd, 2000ul);
			var apiClient = new ArenaClient(amountClient, weightClient, coordinator);

			round.SetPhase(Phase.TransactionSigning);

			// No inputs in the CoinJoin.
			await Assert.ThrowsAsync<ArgumentException>(async () => await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key1, Network.Main), coinjoin));

			coinjoin.Inputs.Add(alice1.Coins.First().Outpoint);

			// Trying to sign coins those are not in the CoinJoin.
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await apiClient.SignTransactionAsync(round.Id, alice2.Coins.ToArray(), new BitcoinSecret(key2, Network.Main), coinjoin));

			coinjoin.Inputs.Add(alice2.Coins.First().Outpoint);

			// Trying to sign coins with the wrong secret.
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key2, Network.Main), coinjoin));

			Assert.False(round.Coinjoin.HasWitness);

			// Trying to sign coins with the wrong secret.
			await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key1, Network.Main), coinjoin);
			Assert.False(round.Coinjoin.Inputs.All(i => i.HasWitScript()));

			await apiClient.SignTransactionAsync(round.Id, alice2.Coins.ToArray(), new BitcoinSecret(key2, Network.Main), coinjoin);
			Assert.True(round.Coinjoin.Inputs.All(i => i.HasWitScript()));
		}
	}
}
