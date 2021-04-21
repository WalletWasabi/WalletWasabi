using Moq;
using NBitcoin;
using System;
using System.Linq;
using System.Threading;
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
using WalletWasabi.WabiSabi.Crypto;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class BobClientTests
	{
		[Fact]
		public async Task RegisterOutputTestAsync()
		{
			var config = new WabiSabiConfig { MaxInputCountByRound = 1 };
			var round = WabiSabiFactory.CreateRound(config);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));

			var km = ServiceFactory.CreateKeyManager("");
			var key = BitcoinFactory.CreateHdPubKey(km);
			SmartCoin coin1 = BitcoinFactory.CreateSmartCoin(key, Money.Coins(2m));
			var outpoint = coin1.OutPoint;

			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetTxOutAsync(outpoint.Hash, (int)outpoint.N, true))
				.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
				{
					IsCoinBase = false,
					Confirmations = coin1.Height,
					TxOut = coin1.TxOut,
				});

			CredentialPool amountCredential = new();
			CredentialPool vsizeCredential = new();

			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);
			var aliceArenaClient = new ArenaClient(round.AmountCredentialIssuerParameters, round.VsizeCredentialIssuerParameters, amountCredential, vsizeCredential, coordinator, new InsecureRandom());
			var bobArenaClient = new ArenaClient(round.AmountCredentialIssuerParameters, round.VsizeCredentialIssuerParameters, amountCredential, vsizeCredential, coordinator, new InsecureRandom());
			Assert.Equal(Phase.InputRegistration, round.Phase);

			var bitcoinSecret = km.GetSecrets("", coin1.ScriptPubKey).Single().PrivateKey.GetBitcoinSecret(Network.Main);
			var aliceClient = await AliceClient.CreateNewAsync(aliceArenaClient, coin1.Coin, bitcoinSecret, round.Id, round.Hash, round.FeeRate);

			Task confirmationTask = aliceClient.ConfirmConnectionAsync(TimeSpan.FromSeconds(3), CancellationToken.None);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
			await confirmationTask;
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			using var destinationKey1 = new Key();
			using var destinationKey2 = new Key();
			using var destinationKey3 = new Key();
			using var destinationKey4 = new Key();

			var bobClient = new BobClient(round.Id, bobArenaClient);
			await bobClient.RegisterOutputAsync(Money.Coins(0.25m), destinationKey1.PubKey.WitHash.ScriptPubKey);
			await bobClient.RegisterOutputAsync(Money.Coins(0.25m), destinationKey2.PubKey.WitHash.ScriptPubKey);
			await bobClient.RegisterOutputAsync(Money.Coins(0.25m), destinationKey3.PubKey.WitHash.ScriptPubKey);
			await bobClient.RegisterOutputAsync(Money.Coins(0.25m), destinationKey4.PubKey.WitHash.ScriptPubKey);

			Assert.Equal(4, round.Bobs.Count);
		}
	}
}
