using Moq;
using NBitcoin;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Controllers;
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
using WalletWasabi.WabiSabi.Models;
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
			var km = ServiceFactory.CreateKeyManager("");
			var key = BitcoinFactory.CreateHdPubKey(km);
			SmartCoin coin1 = BitcoinFactory.CreateSmartCoin(key, Money.Coins(2m));

			var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, mockRpc, round);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));

			ZeroCredentialPool zeroAmountCredential = new();
			ZeroCredentialPool zeroVsizeCredential = new();

			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);
			var insecureRandom = new InsecureRandom();
			var wabiSabiApi = new WabiSabiController(coordinator);
			var roundState = RoundState.FromRound(round);
			var aliceArenaClient = new ArenaClient(
				roundState.CreateAmountCredentialClient(zeroAmountCredential, insecureRandom),
				roundState.CreateVsizeCredentialClient(zeroVsizeCredential, insecureRandom),
				wabiSabiApi);
			var bobArenaClient = new ArenaClient(
				roundState.CreateAmountCredentialClient(zeroAmountCredential, insecureRandom),
				roundState.CreateVsizeCredentialClient(zeroVsizeCredential, insecureRandom),
				wabiSabiApi);
			Assert.Equal(Phase.InputRegistration, round.Phase);

			var bitcoinSecret = km.GetSecrets("", coin1.ScriptPubKey).Single().PrivateKey.GetBitcoinSecret(Network.Main);

			var aliceClient = new AliceClient(round.Id, aliceArenaClient, coin1.Coin, round.FeeRate, bitcoinSecret);
			await aliceClient.RegisterInputAsync(CancellationToken.None);

			Task confirmationTask = aliceClient.ConfirmConnectionAsync(TimeSpan.FromSeconds(1), roundState.MaxVsizeAllocationPerAlice, CancellationToken.None);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
			await confirmationTask;
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			using var destinationKey = new Key();
			var destination = destinationKey.PubKey.WitHash.ScriptPubKey;

			var bobClient = new BobClient(round.Id, bobArenaClient);

			await bobClient.RegisterOutputAsync(Money.Coins(0.25m), destination, aliceClient.RealAmountCredentials, aliceClient.RealVsizeCredentials, CancellationToken.None);

			var bob = Assert.Single(round.Bobs);
			Assert.Equal(destination, bob.Script);
			Assert.Equal(25_000_000, bob.CredentialAmount);
		}
	}
}
