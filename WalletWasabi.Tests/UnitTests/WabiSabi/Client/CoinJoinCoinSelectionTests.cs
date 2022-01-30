using System.Collections.Generic;
using System.Linq;
using Moq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinJoinCoinSelectionTests
{
	[Fact]
	public void SelectEmptySetOfCoins()
	{
		// This test is to make sure no coins are selected when there are no coins.
		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: Enumerable.Empty<SmartCoin>(),
			CreateMultipartyTransactionParameters(),
			consolidationMode: false,
			minAnonScoreTarget: 10,
			ConfigureRng(5));

		Assert.Empty(coins);
	}

	[Fact]
	public void FullyPrivateSetOfCoins()
	{
		// This test is to make sure no coins are selected when all coins are private.
		const int MinAnonimitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var hdpubkey = BitcoinFactory.CreateHdPubKey(km);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet + 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			CreateMultipartyTransactionParameters(),
			consolidationMode: false,
			minAnonScoreTarget: MinAnonimitySet,
			ConfigureRng(5));

		Assert.Empty(coins);
	}

	[Fact]
	public void OnlyOneNonPrivateCoinInBigSetOfCoins()
	{
		const int MinAnonimitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		SmartCoin smallerAnonCoin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: MinAnonimitySet - 1);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), 0, anonymitySet: MinAnonimitySet + 1))
			.Prepend(smallerAnonCoin)
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			CreateMultipartyTransactionParameters(),
			consolidationMode: true,
			minAnonScoreTarget: MinAnonimitySet,
			ConfigureRng(5));

		Assert.Contains(smallerAnonCoin, coins);
		Assert.Equal(10, coins.Count);
	}

	[Fact]
	public void OnlyOneNonPrivateCoinInEmptySetOfCoins()
	{
		// This test is to make sure that we select the only non-private coin when it is the only coin in the wallet.
		const int MinAnonimitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var hdpubkey = BitcoinFactory.CreateHdPubKey(km);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet - 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			CreateMultipartyTransactionParameters(),
			consolidationMode: false,
			minAnonScoreTarget: MinAnonimitySet,
			ConfigureRng(1));

		Assert.Single(coins);
	}

	[Fact]
	public void TwoNonPrivateCoinInSetOfCoins()
	{
		// This test is to make sure that we never select two non-private coins.
		const int MinAnonimitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var hdpubkey = BitcoinFactory.CreateHdPubKey(km);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet - 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			CreateMultipartyTransactionParameters(),
			consolidationMode: false,
			minAnonScoreTarget: MinAnonimitySet,
			ConfigureRng(1));

		Assert.Single(coins);
	}

	private static WasabiRandom ConfigureRng(int returnValue)
	{
		var mockWasabiRandom = new Mock<WasabiRandom>();
		mockWasabiRandom.Setup(r => r.GetInt(It.IsAny<int>(), It.IsAny<int>())).Returns(returnValue);
		return mockWasabiRandom.Object;
	}

	private static MultipartyTransactionParameters CreateMultipartyTransactionParameters()
	{
		var reasonableRange = new MoneyRange(Money.Coins(0.0001m), Money.Coins(430));
		var txParams = new MultipartyTransactionParameters(
			new FeeRate(5m),
			CoordinationFeeRate.Zero,
			reasonableRange,
			reasonableRange,
			Network.Main);
		return txParams;
	}
}
