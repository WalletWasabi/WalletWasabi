using System.Linq;
using Moq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinJoinCoinSelectionTests
{
	[Fact]
	public void SelectNothingFromEmptySetOfCoins()
	{
		// This test is to make sure no coins are selected when there are no coins.
		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: Enumerable.Empty<SmartCoin>(),
			CreateMultipartyTransactionParameters(),
			consolidationMode: false,
			anonScoreTarget: 10,
			ConfigureRng(5));

		Assert.Empty(coins);
	}

	[Fact]
	public void SelectNothingFromFullyPrivateSetOfCoins()
	{
		// This test is to make sure no coins are selected when all coins are private.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: AnonymitySet + 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			CreateMultipartyTransactionParameters(),
			consolidationMode: false,
			anonScoreTarget: AnonymitySet,
			ConfigureRng(5));

		Assert.Empty(coins);
	}

	[Fact]
	public void SelectNonPrivateCoinFromOneNonPrivateCoinInBigSetOfCoinsConsolidationMode()
	{
		// This test is to make sure that we select the non-private coin in the set.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		SmartCoin smallerAnonCoin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: AnonymitySet - 1);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: AnonymitySet + 1))
			.Prepend(smallerAnonCoin)
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			CreateMultipartyTransactionParameters(),
			consolidationMode: true,
			anonScoreTarget: AnonymitySet,
			ConfigureRng(5));

		Assert.Contains(smallerAnonCoin, coins);
		Assert.Equal(10, coins.Count);
	}

	[Fact]
	public void SelectNonPrivateCoinFromOneCoinSetOfCoins()
	{
		// This test is to make sure that we select the only non-private coin when it is the only coin in the wallet.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: AnonymitySet - 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			CreateMultipartyTransactionParameters(),
			consolidationMode: false,
			anonScoreTarget: AnonymitySet,
			ConfigureRng(1));

		Assert.Single(coins);
	}

	[Fact]
	public void SelectOneNonPrivateCoinFromTwoCoinsSetOfCoins()
	{
		// This test is to make sure that we select only one non-private coin.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: AnonymitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: AnonymitySet - 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			CreateMultipartyTransactionParameters(),
			consolidationMode: false,
			anonScoreTarget: AnonymitySet,
			ConfigureRng(1));

		Assert.Single(coins);
	}

	[Fact]
	public void SelectTwoNonPrivateCoinsFromTwoCoinsSetOfCoinsConsolidationMode()
	{
		// This test is to make sure that we select more than one non-private coin.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: AnonymitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: AnonymitySet - 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			CreateMultipartyTransactionParameters(),
			consolidationMode: true,
			anonScoreTarget: AnonymitySet,
			ConfigureRng(1));

		Assert.Equal(2, coins.Count);
	}

	private static WasabiRandom ConfigureRng(int returnValue)
	{
		var mockWasabiRandom = new Mock<WasabiRandom>();
		mockWasabiRandom.Setup(r => r.GetInt(It.IsAny<int>(), It.IsAny<int>())).Returns(returnValue);
		return mockWasabiRandom.Object;
	}

	private static RoundParameters CreateMultipartyTransactionParameters()
	{
		var roundParams = WabiSabiFactory.CreateRoundParameters(new()
		{
			MinRegistrableAmount = Money.Coins(0.0001m),
			MaxRegistrableAmount = Money.Coins(430)
		});
		return roundParams;
	}
}
