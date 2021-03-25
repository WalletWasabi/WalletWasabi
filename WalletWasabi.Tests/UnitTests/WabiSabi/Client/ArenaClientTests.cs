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
			var amountClient = new WabiSabiClient(round.AmountCredentialIssuerParameters, protocolCredentialNumber, rnd, 4_300_000_000_000ul); // TODO: remove  hardcoded max value
			var weightClient = new WabiSabiClient(round.WeightCredentialIssuerParameters, protocolCredentialNumber, rnd, 1_000ul); // TODO: remove  hardcoded max value

			var apiClient = new ArenaClient(amountClient, weightClient, coordinator);

			var aliceId = await apiClient.RegisterInputAsync(Money.Coins(1m), outpoint, key, round.Id, round.Hash);

			Assert.NotEqual(Guid.Empty, aliceId);
			Assert.Empty(apiClient.AmountCredentialClient.Credentials.Valuable);

			var reissuanceAmounts = new[]
			{
				Money.Coins(.75m) - round.FeeRate.GetFee(Constants.P2wpkhInputVirtualSize),
				Money.Coins(.25m)
			};

			// Phase: Input Registration
			await apiClient.ConfirmConnectionAsync(
				round.Id,
				aliceId,
				apiClient.AmountCredentialClient.Credentials.ZeroValue.Take(protocolCredentialNumber),
				reissuanceAmounts);

			Assert.Empty(apiClient.AmountCredentialClient.Credentials.Valuable);

			// Phase: Connection Confirmation
			round.SetPhase(Phase.ConnectionConfirmation);
			await apiClient.ConfirmConnectionAsync(
				round.Id,
				aliceId,
				apiClient.AmountCredentialClient.Credentials.ZeroValue.Take(protocolCredentialNumber),
				reissuanceAmounts);

			Assert.Single(apiClient.AmountCredentialClient.Credentials.Valuable, x => x.Amount.ToMoney() == reissuanceAmounts.First());
			Assert.Single(apiClient.AmountCredentialClient.Credentials.Valuable, x => x.Amount.ToMoney() == reissuanceAmounts.Last());
		}
	}
}
