using System.Collections.Generic;
using System.Linq;
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
		using SecureRandom rnd = new();
		var coins = CoinJoinClient.SelectCoinsForRound(
				coins: Enumerable.Empty<SmartCoin>(),
				CreateMultipartyTransactionParameters(),
				consolidationMode: false,
				minAnonScoreTarget: 10,
				rnd);

		Assert.Empty(coins);
	}

	[Fact]
	public void FullyPrivateSetOfCoins()
	{
		// This test is to make sure no coins are selected when all coins are private.
		const int MinAnonimitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var hdpubkey = BitcoinFactory.CreateHdPubKey(km);
		var privateCoins = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet + 1))
			.ToList();

		using SecureRandom rnd = new();
		var coins = CoinJoinClient.SelectCoinsForRound(
				coins: privateCoins,
				CreateMultipartyTransactionParameters(),
				consolidationMode: false,
				minAnonScoreTarget: MinAnonimitySet,
				rnd);

		Assert.Empty(coins);
	}

	[Fact]
	public void OnlyOneNonPrivateCoinInBigSetOfCoins()
	{
		// This test is to make sure that we don't select any coin when there is only one non-private coin in the set.
		const int MinAnonimitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var hdpubkey = BitcoinFactory.CreateHdPubKey(km);
		var privateCoins = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet + 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet - 1))
			.ToList();

		using SecureRandom rnd = new();
		var coins = CoinJoinClient.SelectCoinsForRound(
				coins: privateCoins,
				CreateMultipartyTransactionParameters(),
				consolidationMode: true,
				minAnonScoreTarget: MinAnonimitySet,
				rnd);

		Assert.Empty(coins);
	}

	[Fact]
	public void OnlyOneNonPrivateCoinInEmptySetOfCoins()
	{
		// This test is to make sure that we don't select the only non-private coin when it is the only coin in the wallaet.
		const int MinAnonimitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var hdpubkey = BitcoinFactory.CreateHdPubKey(km);
		var privateCoins = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet - 1))
			.ToList();

		using SecureRandom rnd = new();
		var coins = CoinJoinClient.SelectCoinsForRound(
				coins: privateCoins,
				CreateMultipartyTransactionParameters(),
				consolidationMode: false,
				minAnonScoreTarget: MinAnonimitySet,
				rnd);

		Assert.Empty(coins);
	}


	[Fact]
	public void TwoNonPrivateCoinInSetOfCoins()
	{
		// This test is to make sure that we never select two non-private coins.
		const int MinAnonimitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var hdpubkey = BitcoinFactory.CreateHdPubKey(km);
		var privateCoins = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(hdpubkey, Money.Coins(1m), 0, anonymitySet: MinAnonimitySet - 1))
			.ToList();

		using SecureRandom rnd = new();
		var coins = CoinJoinClient.SelectCoinsForRound(
				coins: privateCoins,
				CreateMultipartyTransactionParameters(),
				consolidationMode: false,
				minAnonScoreTarget: MinAnonimitySet,
				rnd);

		Assert.Single(coins);
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
