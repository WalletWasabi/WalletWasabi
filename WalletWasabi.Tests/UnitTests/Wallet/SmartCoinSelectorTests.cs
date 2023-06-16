using System.Collections.Generic;
using System.Linq;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class SmartCoinSelectorTests
{
	public SmartCoinSelectorTests()
	{
		KeyManager = KeyManager.Recover(
			new Mnemonic("all all all all all all all all all all all all"),
			"",
			Network.Main,
			KeyManager.GetAccountKeyPath(Network.Main, ScriptPubKeyType.Segwit));
	}

	private KeyManager KeyManager { get; }
	private static IEnumerable<Coin> EmptySuggestion { get; } = Enumerable.Empty<Coin>();

	[Fact]
	public void SelectsOnlyOneCoinWhenPossible()
	{
		decimal target = 0.3m;
		List<SmartCoin> availableCoins = GenerateSmartCoins(Enumerable.Range(0, 9).Select(i => ("Juan", 0.1m * (i + 1))));

		SmartCoinSelector selector = new(availableCoins);
		IEnumerable<ICoin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, Money.Coins(target));

		Coin theOnlyOne = Assert.Single(coinsToSpend.Cast<Coin>());
		Assert.Equal(target, theOnlyOne.Amount.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void DontSelectUnnecessaryInputs()
	{
		Money target = Money.Coins(4m);
		List<SmartCoin> availableCoins = GenerateSmartCoins(Enumerable.Range(0, 10).Select(i => ("Juan", 0.1m * (i + 1))));

		SmartCoinSelector selector = new(availableCoins);
		List<Coin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(5, coinsToSpend.Count);
		Assert.Equal(target, Money.Satoshis(coinsToSpend.Sum(x => x.Amount)));
	}

	[Fact]
	public void PreferSameClusterOverExactAmount()
	{
		Money target = Money.Coins(0.3m);
		List<SmartCoin> availableCoins = GenerateSmartCoins(("Besos", 0.2m), ("Besos", 0.2m), ("Juan", 0.1m), ("Juan", 0.1m));

		SmartCoinSelector selector = new(availableCoins);
		List<Coin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		// We do NOT expect an exact match, because that would mix the clusters.
		Assert.Equal(Money.Coins(0.4m), Money.Satoshis(coinsToSpend.Sum(x => x.Amount)));
	}

	[Fact]
	public void PreferExactAmountWhenClustersAreDifferent()
	{
		Money target = Money.Coins(0.3m);
		List<SmartCoin> availableCoins = GenerateSmartCoins(("Besos", 0.2m), ("Juan", 0.1m), ("Adam", 0.2m), ("Eve", 0.1m));

		SmartCoinSelector selector = new(availableCoins);
		List<Coin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(target, Money.Satoshis(coinsToSpend.Sum(x => x.Amount)));       // Cluster-privacy is indifferent, so aim for exact amount.
	}

	[Fact]
	public void DontUseTheWholeClusterIfNotNecessary()
	{
		Money target = Money.Coins(0.3m);
		List<SmartCoin> availableCoins = GenerateDuplicateSmartCoins(("Juan", 0.1m), count: 10);

		SmartCoinSelector selector = new(availableCoins);
		List<Coin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(3, coinsToSpend.Count);
		Assert.Equal(target, Money.Satoshis(coinsToSpend.Sum(x => x.Amount)));
	}

	[Fact]
	public void PreferLessCoinsOnSameAmount()
	{
		Money target = Money.Coins(1m);
		List<SmartCoin> availableCoins = GenerateDuplicateSmartCoins(("Juan", 0.1m), count: 11);
		availableCoins.Add(GenerateDuplicateSmartCoins(("Beto", 0.2m), count: 5));

		SmartCoinSelector selector = new(availableCoins);
		List<Coin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(5, coinsToSpend.Count);
		Assert.Equal(target, Money.Satoshis(coinsToSpend.Sum(x => x.Amount)));
	}

	[Fact]
	public void PreferLessCoinsOverExactAmount()
	{
		Money target = Money.Coins(0.41m);
		var smartCoins = GenerateSmartCoins(Enumerable.Range(0, 10).Select(i => ("Juan", 0.1m * (i + 1))));
		smartCoins.Add(BitcoinFactory.CreateSmartCoin(smartCoins[0].HdPubKey, 0.11m));
		var someCoins = smartCoins.Select(x => x.Coin);

		var selector = new SmartCoinSelector(smartCoins);
		var coinsToSpend = selector.Select(someCoins, target);

		var theOnlyOne = Assert.Single(coinsToSpend.Cast<Coin>());
		Assert.Equal(0.5m, theOnlyOne.Amount.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void PreferSameScript()
	{
		Money target = Money.Coins(0.31m);
		var smartCoins = GenerateSmartCoins(Enumerable.Repeat(("Juan", 0.2m), 12)).ToList();
		smartCoins.Add(BitcoinFactory.CreateSmartCoin(smartCoins[0].HdPubKey, 0.11m));

		var selector = new SmartCoinSelector(smartCoins);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(coinsToSpend[0].ScriptPubKey, coinsToSpend[1].ScriptPubKey);
		Assert.Equal(0.31m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Fact]
	public void PreferMorePrivateClusterScript()
	{
		Money target = Money.Coins(0.3m);
		var coinsKnownByJuan = GenerateSmartCoins(Enumerable.Repeat(("Juan", 0.2m), 5));
		var coinsKnownByBeto = GenerateSmartCoins(Enumerable.Repeat(("Beto", 0.2m), 2));

		var selector = new SmartCoinSelector(coinsKnownByJuan.Concat(coinsKnownByBeto).ToList());
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(0.4m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	private List<SmartCoin> GenerateDuplicateSmartCoins((string Cluster, decimal amount) coin, int count)
		=> GenerateSmartCoins(Enumerable.Range(start: 0, count).Select(x => coin));

	private List<SmartCoin> GenerateSmartCoins(params (string Cluster, decimal amount)[] coins)
		=> GenerateSmartCoins((IEnumerable<(string Cluster, decimal amount)>)coins);

	private List<SmartCoin> GenerateSmartCoins(IEnumerable<(string Cluster, decimal amount)> coins)
	{
		Dictionary<string, List<(HdPubKey key, decimal amount)>> generatedKeyGroup = new();

		// Create cluster-grouped keys
		foreach (var targetCoin in coins)
		{
			var key = KeyManager.GenerateNewKey(new LabelsArray(targetCoin.Cluster), KeyState.Clean, false);

			if (!generatedKeyGroup.ContainsKey(targetCoin.Cluster))
			{
				generatedKeyGroup.Add(targetCoin.Cluster, new());
			}

			generatedKeyGroup[targetCoin.Cluster].Add((key, targetCoin.amount));
		}

		var coinPairClusters = generatedKeyGroup.GroupBy(x => x.Key)
			.Select(x => x.Select(y => y.Value)) // Group the coin pairs into clusters.
			.SelectMany(x => x
				.Select(coinPair => (coinPair,
					cluster: new Cluster(coinPair.Select(z => z.key))))).ToList();

		// Set each key with its corresponding cluster object.
		foreach (var x in coinPairClusters)
		{
			foreach (var y in x.coinPair)
			{
				y.key.Cluster = x.cluster;
			}
		}

		return coinPairClusters.Select(x => x.coinPair)
			.SelectMany(x =>
				x.Select(y => BitcoinFactory.CreateSmartCoin(y.key, y.amount)))
			.ToList(); // Generate the final SmartCoins.
	}
}
