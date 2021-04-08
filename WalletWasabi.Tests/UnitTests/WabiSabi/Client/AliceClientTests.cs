using Moq;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class AliceClientTests
	{
		[Fact]
		public async Task CreateNewAsync()
		{
			var config = new WabiSabiConfig { MaxInputCountByRound = 1 };
			var round = WabiSabiFactory.CreateRound(config);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

			var km = ServiceFactory.CreateKeyManager("");
			var key = BitcoinFactory.CreateHdPubKey(km);
			SmartCoin coin1 = BitcoinFactory.CreateSmartCoin(key, Money.Coins(1m));
			var outpoint = coin1.OutPoint;

			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetTxOutAsync(outpoint.Hash, (int)outpoint.N, true))
				.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
				{
					IsCoinBase = false,
					Confirmations = coin1.Height,
					TxOut = coin1.TxOut,
				});

			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);
			var apiClient = new ArenaClient(round.AmountCredentialIssuerParameters, round.WeightCredentialIssuerParameters, coordinator, new InsecureRandom());

			var bitcoinSecret = new BitcoinSecret(km.EncryptedSecret.GetKey(""), Network.Main);
			AliceClient aliceClient = await AliceClient.CreateNewAsync(apiClient, new[] { coin1.Coin }, bitcoinSecret, round.Id, round.Hash);
		}
	}
}
