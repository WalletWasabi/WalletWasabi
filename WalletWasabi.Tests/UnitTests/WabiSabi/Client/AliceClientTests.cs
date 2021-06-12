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
	public class AliceClientTests
	{
		[Fact]
		public async Task CreateNewAsync()
		{
			var config = new WabiSabiConfig { MaxInputCountByRound = 1 };
			var round = WabiSabiFactory.CreateRound(config);
			var km = ServiceFactory.CreateKeyManager("");
			var key = BitcoinFactory.CreateHdPubKey(km);
			SmartCoin coin1 = BitcoinFactory.CreateSmartCoin(key, Money.Coins(1m));
			var outpoint = coin1.OutPoint;

			var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, mockRpc, round);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));

			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);
			var wabiSabiApi = new WabiSabiController(coordinator);

			ZeroCredentialPool amountCredentialPool = new();
			ZeroCredentialPool vsizeCredentialPool = new();
			var insecureRandom = new InsecureRandom();
			var roundState = RoundState.FromRound(round);
			var arenaClient = new ArenaClient(
				roundState.CreateAmountCredentialClient(amountCredentialPool, insecureRandom),
				roundState.CreateVsizeCredentialClient(vsizeCredentialPool, insecureRandom),
				wabiSabiApi);
			Assert.Equal(Phase.InputRegistration, arena.Rounds.First().Phase);

			var bitcoinSecret = km.GetSecrets("", coin1.ScriptPubKey).Single().PrivateKey.GetBitcoinSecret(Network.Main);

			var aliceClient = new AliceClient(round.Id, arenaClient, coin1.Coin, round.FeeRate, bitcoinSecret);
			await aliceClient.RegisterInputAsync(CancellationToken.None);

			Task confirmationTask = aliceClient.ConfirmConnectionAsync(TimeSpan.FromSeconds(1), roundState.MaxVsizeAllocationPerAlice, CancellationToken.None);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
			await confirmationTask;

			Assert.Equal(Phase.ConnectionConfirmation, arena.Rounds.First().Phase);
		}
	}
}
