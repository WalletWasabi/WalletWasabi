using System.Linq;
using Moq;
using NBitcoin;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
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
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, ConfigureRng(5));
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: Enumerable.Empty<SmartCoin>(),
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

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

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, ConfigureRng(5));
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	[Fact]
	public void SelectNothingFromTooSmallCoin()
	{
		// This test is to make sure no coins are selected when there too small coins.
		// Although the coin amount is larger than the smallest reasonable effective denomination, if the algorithm is right, then the effective input amount is considered.
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = new[] { BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00017423m), anonymitySet: 1) };
		var roundParams = WabiSabiFactory.CreateRoundParameters(new()
		{
			MinRegistrableAmount = Money.Coins(0.0001m),
			MaxRegistrableAmount = Money.Coins(430),
		});

		Assert.Equal(Money.Coins(0.00017422m), roundParams.CalculateSmallestReasonableDenomination());

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, ConfigureRng(5));
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(roundParams),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	[Fact]
	public void SelectNothingFromTooSmallSetOfCoins()
	{
		// This test is to make sure no coins are selected when there too small coins.
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = new[]
		{
			BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00008711m + 0.00006900m), anonymitySet: 1),
			BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00008710m + 0.00006900m), anonymitySet: 1)
		};
		var roundParams = WabiSabiFactory.CreateRoundParameters(new()
		{
			MinRegistrableAmount = Money.Coins(0.0001m),
			MaxRegistrableAmount = Money.Coins(430),
		});

		Assert.Equal(Money.Coins(0.00017422m), roundParams.CalculateSmallestReasonableDenomination());

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, ConfigureRng(5));
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(roundParams),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	[Fact]
	public void SelectSomethingFromJustEnoughSetOfCoins()
	{
		// This test is to make sure the coins are selected when the selection's effective sum is exactly the smallest reasonable effective denom.
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = new[]
		{
			BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00008711m + 0.00006900m), anonymitySet: 1),
			BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00008711m + 0.00006900m), anonymitySet: 1)
		};
		var roundParams = WabiSabiFactory.CreateRoundParameters(new()
		{
			MinRegistrableAmount = Money.Coins(0.0001m),
			MaxRegistrableAmount = Money.Coins(430),
		});

		Assert.Equal(Money.Coins(0.00017422m), roundParams.CalculateSmallestReasonableDenomination());

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, ConfigureRng(5));
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(roundParams),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.NotEmpty(coins);
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

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: true, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, ConfigureRng(5));
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

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

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, ConfigureRng(1));
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

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

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, ConfigureRng(1));
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

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

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: true, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, ConfigureRng(1));
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			UtxoSelectionParameters.FromRoundParameters(CreateMultipartyTransactionParameters()),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

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
