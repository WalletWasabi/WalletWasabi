using System.Linq;
using Moq;
using NBitcoin;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

/// <summary>
/// Tests for <see cref="CoinJoinCoinSelector"/>.
/// </summary>
public class CoinJoinCoinSelectionTests
{
	/// <summary>
	/// This test is to make sure no coins are selected when there are no coins.
	/// </summary>
	[Fact]
	public void SelectNothingFromEmptySetOfCoins()
	{
		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: Enumerable.Empty<SmartCoin>(),
			true,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	/// <summary>
	/// This test is to make sure no coins are selected when all coins are private.
	/// </summary>
	[Fact]
	public void SelectNothingFromFullyPrivateSetOfCoins()
	{
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		// We gotta make sure the distance from external keys is sufficient.
		foreach (var sc in coinsToSelectFrom)
		{
			var sci = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1);
			sci.Transaction.TryAddWalletInput(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1));
			sc.Transaction.TryAddWalletInput(sci);
		}
		foreach (var sc in coinsToSelectFrom)
		{
			BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(sc);
		}

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);

		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	/// <summary>
	/// This test is to make sure no coins are selected when there too small coins.
	/// Although the coin amount is larger than the smallest reasonable effective denomination, if the algorithm is right, then the effective input amount is considered.
	/// </summary>
	[Fact]
	public void SelectSomethingFromPrivateButExternalSetOfCoins1()
	{
		// Although all coins have reached the desired anonymity set, they are not sufficiently distanced from external keys, because they are external keys.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km, isInternal: false), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.NotEmpty(coins);
	}


	[Fact]
	public void SelectSomethingFromPrivateButNotDistancedSetOfCoins2()
	{
		// Although all coins have reached the desired anonymity set, they are not sufficiently distanced from external keys.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.NotEmpty(coins);
	}

	[Fact]
	public void SelectSomethingFromPrivateButExternalSetOfCoins3()
	{
		// Although all coins have reached the desired anonymity set, they are not sufficiently distanced from external keys.
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km, isInternal: true), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.ToList();

		// We gotta make sure the distance from external keys is sufficient.
		foreach (var sc in coinsToSelectFrom)
		{
			sc.Transaction.TryAddWalletInput(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km, isInternal: false), Money.Coins(1m), anonymitySet: AnonymitySet + 1));
		}
		foreach (var sc in coinsToSelectFrom)
		{
			BlockchainAnalyzer.SetIsSufficientlyDistancedFromExternalKeys(sc);
		}

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);
		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.NotEmpty(coins);
	}

	[Fact]
	public void SelectNothingFromTooSmallCoin()
	{
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = new[] { BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(0.00017423m), anonymitySet: 1) };
		var roundParams = WabiSabiFactory.CreateRoundParameters(new()
		{
			MinRegistrableAmount = Money.Coins(0.0001m),
			MaxRegistrableAmount = Money.Coins(430),
		});

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			UtxoSelectionParameters.FromRoundParameters(roundParams, [ScriptType.P2WPKH, ScriptType.Taproot]),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	/// <summary>
	/// This test is to make sure no coins are selected when there too small coins.
	/// </summary>
	[Fact]
	public void SelectNothingFromTooSmallSetOfCoins()
	{
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

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			UtxoSelectionParameters.FromRoundParameters(roundParams, [ScriptType.P2WPKH, ScriptType.Taproot]),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Empty(coins);
	}

	/// <summary>
	/// This test is to make sure the coins are selected when the selection's effective sum is exactly the smallest reasonable effective denom.
	/// </summary>
	[Fact]
	public void SelectSomethingFromJustEnoughSetOfCoins()
	{
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

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: 10, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			UtxoSelectionParameters.FromRoundParameters(roundParams, [ScriptType.P2WPKH, ScriptType.Taproot]),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.NotEmpty(coins);
	}

	/// <summary>
	/// This test is to make sure that we select the non-private coin in the set.
	/// </summary>
	[Fact]
	public void SelectNonPrivateCoinFromOneNonPrivateCoinInBigSetOfCoinsConsolidationMode()
	{
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		SmartCoin smallerAnonCoin = BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1);
		var coinsToSelectFrom = Enumerable
			.Range(0, 10)
			.Select(i => BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet + 1))
			.Prepend(smallerAnonCoin)
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 5);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: true, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Contains(smallerAnonCoin, coins);
		Assert.Equal(10, coins.Count);
	}

	/// <summary>
	/// This test is to make sure that we select the only non-private coin when it is the only coin in the wallet.
	/// </summary>
	[Fact]
	public void SelectNonPrivateCoinFromOneCoinSetOfCoins()
	{
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 10);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Single(coins);
	}

	/// <summary>
	/// This test is to make sure that we select more non-private coins when they are coming from different txs.
	/// </summary>
	/// <remarks>Note randomization can make this test fail even though that's unlikely.</remarks>
	[Fact]
	public void SelectMoreNonPrivateCoinFromTwoCoinsSetOfCoins()
	{
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 10, sameTxAllowance: 0);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: false, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Equal(2, coins.Count);
	}

	/// <summary>
	/// This test is to make sure that we select more than one non-private coin.
	/// </summary>
	[Fact]
	public void SelectTwoNonPrivateCoinsFromTwoCoinsSetOfCoinsConsolidationMode()
	{
		const int AnonymitySet = 10;
		var km = KeyManager.CreateNew(out _, "", Network.Main);
		var coinsToSelectFrom = Enumerable
			.Empty<SmartCoin>()
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.Prepend(BitcoinFactory.CreateSmartCoin(BitcoinFactory.CreateHdPubKey(km), Money.Coins(1m), anonymitySet: AnonymitySet - 1))
			.ToList();

		CoinJoinCoinSelectorRandomnessGenerator generator = CreateSelectorGenerator(inputTarget: 10);

		var coinJoinCoinSelector = new CoinJoinCoinSelector(consolidationMode: true, anonScoreTarget: AnonymitySet, semiPrivateThreshold: 0, generator);
		var coins = coinJoinCoinSelector.SelectCoinsForRound(
			coins: coinsToSelectFrom,
			true,
			CreateUtxoSelectionParameters(),
			liquidityClue: Constants.MaximumNumberOfBitcoinsMoney);

		Assert.Equal(2, coins.Count);
	}

	private static CoinJoinCoinSelectorRandomnessGenerator CreateSelectorGenerator(int inputTarget, int? sameTxAllowance = null)
	{
		WasabiRandom rng = InsecureRandom.Instance;
		Mock<CoinJoinCoinSelectorRandomnessGenerator> mockGenerator = new(MockBehavior.Loose, CoinJoinCoinSelector.MaxInputsRegistrableByWallet, rng) { CallBase = true };
		_ = mockGenerator.Setup(c => c.GetInputTarget())
			.Returns(inputTarget);

		if (sameTxAllowance is not null)
		{
			_ = mockGenerator.Setup(c => c.GetRandomBiasedSameTxAllowance(It.IsAny<int>()))
				.Returns(sameTxAllowance.Value);
		}

		return mockGenerator.Object;
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

	private static UtxoSelectionParameters CreateUtxoSelectionParameters() =>
		UtxoSelectionParameters.FromRoundParameters(
			CreateMultipartyTransactionParameters(),
			[ScriptType.P2WPKH, ScriptType.Taproot]);
}
