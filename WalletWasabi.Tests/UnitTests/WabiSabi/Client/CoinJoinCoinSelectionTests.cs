using System.Linq;
using Moq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
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
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			consolidationMode: false,
			anonScoreTarget: 10,
			semiPrivateThreshold: 0,
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney,
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
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			consolidationMode: false,
			anonScoreTarget: AnonymitySet,
			semiPrivateThreshold: 0,
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney,
			ConfigureRng(5));

		Assert.Empty(coins);
	}

	[Fact]
	public void SelectNonPrivateCoinFromOneNonPrivateCoinInBigSetOfCoinsConsolidationMode()
	{
		// This test is to make sure that we select the non-private coin in the set.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		SmartCoin smallerAnonCoin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.Prepend(smallerAnonCoin)
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			consolidationMode: true,
			anonScoreTarget: AnonymitySet,
			semiPrivateThreshold: 0,
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney,
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
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			consolidationMode: false,
			anonScoreTarget: AnonymitySet,
			semiPrivateThreshold: 0,
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney,
			ConfigureRng(1));

		Assert.Single(coins);
	}

	[Fact]
	public void SelectMoreNonPrivateCoinFromTwoCoinsSetOfCoins()
	{
		// This test is to make sure that we select more non-private coins when they are coming from different txs.
		// Note randomization can make this test fail even though that's unlikely.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			consolidationMode: false,
			anonScoreTarget: AnonymitySet,
			semiPrivateThreshold: 0,
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney,
			ConfigureRng(1));

		Assert.Equal(2, coins.Count);
	}

	[Fact]
	public void SelectTwoNonPrivateCoinsFromTwoCoinsSetOfCoinsConsolidationMode()
	{
		// This test is to make sure that we select more than one non-private coin.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			consolidationMode: true,
			anonScoreTarget: AnonymitySet,
			semiPrivateThreshold: 0,
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney,
			ConfigureRng(1));

		Assert.Equal(2, coins.Count);
	}

	[Fact]
	public void DoNotSelectCoinsWithBigAnonymityLoss()
	{
		// This test ensures that we do not select coins whose anonymity could be lowered a lot
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var bigCoinWithSmallAnonymity1 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: 1);
		var bigCoinWithSmallAnonymity2 = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: 2);
		var smallCoinWithBigAnonymity = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.1m), anonymitySet: 6);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Append(bigCoinWithSmallAnonymity1)
			.Append(bigCoinWithSmallAnonymity2)
			.Append(smallCoinWithBigAnonymity)
			.ToList();

		var coins = CoinJoinClient.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			consolidationMode: true,
			anonScoreTarget: AnonymitySet,
			semiPrivateThreshold: 0,
			liquidityClue: Money.Coins(0.5m),
			ConfigureRng(1));

		Assert.False(coins.Contains(bigCoinWithSmallAnonymity1) && coins.Contains(smallCoinWithBigAnonymity));
		Assert.False(coins.Contains(bigCoinWithSmallAnonymity2) && coins.Contains(smallCoinWithBigAnonymity));
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
