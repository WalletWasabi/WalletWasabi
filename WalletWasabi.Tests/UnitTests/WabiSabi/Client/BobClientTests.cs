using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Controllers;
using WalletWasabi.Backend.Controllers.WabiSabi;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

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
		using Arena arena = await ArenaBuilder.From(config).With(mockRpc).CreateAndStartAsync(round);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var idempotencyRequestCache = new IdempotencyRequestCache(memoryCache);

		using CoinJoinFeeRateStatStore coinJoinFeeRateStatStore = new(config, arena.Rpc);
		var wabiSabiApi = new WabiSabiController(idempotencyRequestCache, arena, coinJoinFeeRateStatStore);

		InsecureRandom insecureRandom = InsecureRandom.Instance;
		var roundState = RoundState.FromRound(round);
		var aliceArenaClient = new ArenaClient(
			roundState.CreateAmountCredentialClient(insecureRandom),
			roundState.CreateVsizeCredentialClient(insecureRandom),
			wabiSabiApi);
		var bobArenaClient = new ArenaClient(
			roundState.CreateAmountCredentialClient(insecureRandom),
			roundState.CreateVsizeCredentialClient(insecureRandom),
			wabiSabiApi);
		Assert.Equal(Phase.InputRegistration, round.Phase);

		using RoundStateUpdater roundStateUpdater = new(TimeSpan.FromSeconds(2), wabiSabiApi);
		await roundStateUpdater.StartAsync(CancellationToken.None);

		var keyChain = new KeyChain(km, new Kitchen(""));
		var task = AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), aliceArenaClient, coin1, keyChain, roundStateUpdater, CancellationToken.None);

		do
		{
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
		}
		while (round.Phase != Phase.ConnectionConfirmation);

		var aliceClient = await task;

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
		Assert.Equal(Phase.OutputRegistration, round.Phase);

		using var destinationKey = new Key();
		var destination = destinationKey.PubKey.WitHash.ScriptPubKey;

		var bobClient = new BobClient(round.Id, bobArenaClient);

		await bobClient.RegisterOutputAsync(
			destination,
			aliceClient.IssuedAmountCredentials.Take(ProtocolConstants.CredentialNumber),
			aliceClient.IssuedVsizeCredentials.Take(ProtocolConstants.CredentialNumber),
			CancellationToken.None);

		var bob = Assert.Single(round.Bobs);
		Assert.Equal(destination, bob.Script);

		var credentialAmountSum = aliceClient.IssuedAmountCredentials.Take(ProtocolConstants.CredentialNumber).Sum(x => x.Value);
		Assert.Equal(credentialAmountSum, bob.CredentialAmount);
	}
}
