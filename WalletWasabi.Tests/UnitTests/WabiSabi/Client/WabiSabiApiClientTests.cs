using Moq;
using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
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
	public class WabiSabiApiClientTests
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
			await using var coordinator = new PostRequestHandler(config, new Prison(), arena, mockRpc.Object);

			var rnd = new InsecureRandom();
			var amountClient = new WabiSabiClient(round.AmountCredentialIssuerParameters, 2, rnd, 4300000000000ul);
			var weightClient = new WabiSabiClient(round.WeightCredentialIssuerParameters, 2, rnd, 2000ul);

			var apiClient = new WabiSabiApiClient(amountClient, weightClient, coordinator);

			await apiClient.RegisterInputAsync(Money.Coins(1m), outpoint, key, round.Id, round.Hash);
		}

		[Fact]
		public async Task SignTransactionAsync()
		{
			WabiSabiConfig config = new();
			Round round = WabiSabiFactory.CreateRound(config);
			using Key key1 = new();
			Alice alice1 = WabiSabiFactory.CreateAlice(key: key1);

			round.Alices.Add(alice1);

			var coinjoin = round.Coinjoin;
			coinjoin.Inputs.Add(alice1.Coins.First().Outpoint);

			round.SetPhase(Phase.TransactionSigning);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);

			var mockRpc = new Mock<IRPCClient>();
			await using var coordinator = new PostRequestHandler(config, new Prison(), arena, mockRpc.Object);

			var signedCoinJoin = coinjoin.Clone();

			var rnd = new InsecureRandom();
			var amountClient = new WabiSabiClient(round.AmountCredentialIssuerParameters, 2, rnd, 4300000000000ul);
			var weightClient = new WabiSabiClient(round.WeightCredentialIssuerParameters, 2, rnd, 2000ul);
			var apiClient = new WabiSabiApiClient(amountClient, weightClient, coordinator);

			Assert.False(round.Coinjoin.HasWitness);

			await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key1, Network.Main), coinjoin);

			Assert.True(round.Coinjoin.HasWitness);
		}
	}
}
