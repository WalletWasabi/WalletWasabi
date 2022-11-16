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
			KeyManager.GetAccountKeyPath(Network.Main));
	}

	private KeyManager KeyManager { get; }

	[Fact]
	public void SelectsOnlyOneCoinWhenPossible()
	{
		var availableCoins = GenerateSmartCoins(
			Enumerable.Range(0, 9).Select(i => ("Juan", 0.1m * (i + 1))))
			.ToList();

		var selector = new SmartCoinSelector(availableCoins);
		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), Money.Coins(0.3m));

		var theOnlyOne = Assert.Single(coinsToSpend.Cast<Coin>());
		Assert.Equal(0.3m, theOnlyOne.Amount.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void DontSelectUnnecessaryInputs()
	{
		var target = Money.Coins(4m);

		var availableCoins = GenerateSmartCoins(
			Enumerable.Range(0, 10).Select(i => ("Juan", 0.1m * (i + 1))))
			.ToList();

		var selector = new SmartCoinSelector(availableCoins);
		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), target);

		Assert.Equal(5, coinsToSpend.Count());
	}

	[Fact]
	public void PreferLessCoinsOnSameAmount()
	{
		var target = Money.Coins(1m);

		var availableCoins = GenerateSmartCoins(
			Enumerable.Range(0, 10).Select(i => ("Juan", 0.1m)))
			.ToList();

		availableCoins.Add(GenerateSmartCoins(
			Enumerable.Range(0, 5).Select(i => ("Beto", 0.2m)))
			.ToList());

		var selector = new SmartCoinSelector(availableCoins);
		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), target);

		Assert.Equal(5, coinsToSpend.Count());
		Assert.True(coinsToSpend.Cast<Coin>().Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)) >= target.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void PreferLessCoinsOverExactAmount()
	{
		var smartCoins = GenerateSmartCoins(
			Enumerable.Range(0, 10).Select(i => ("Juan", 0.1m * (i + 1))))
			.ToList();

		smartCoins.Add(BitcoinFactory.CreateSmartCoin(smartCoins[0].HdPubKey, 0.11m));

		var selector = new SmartCoinSelector(smartCoins);

		var someCoins = smartCoins.Select(x => x.Coin);
		var coinsToSpend = selector.Select(someCoins, Money.Coins(0.41m));

		var theOnlyOne = Assert.Single(coinsToSpend.Cast<Coin>());
		Assert.Equal(0.5m, theOnlyOne.Amount.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void PreferSameScript()
	{
		var smartCoins = GenerateSmartCoins(Enumerable.Repeat(("Juan", 0.2m), 12)).ToList();

		smartCoins.Add(BitcoinFactory.CreateSmartCoin(smartCoins[0].HdPubKey, 0.11m));

		var selector = new SmartCoinSelector(smartCoins);

		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), Money.Coins(0.31m)).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(coinsToSpend[0].ScriptPubKey, coinsToSpend[1].ScriptPubKey);
		Assert.Equal(0.31m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Fact]
	public void PreferMorePrivateClusterScript()
	{
		var coinsKnownByJuan = GenerateSmartCoins(Enumerable.Repeat(("Juan", 0.2m), 5));

		var coinsKnownByBeto = GenerateSmartCoins(Enumerable.Repeat(("Beto", 0.2m), 2));

		var selector = new SmartCoinSelector(coinsKnownByJuan.Concat(coinsKnownByBeto).ToList());
		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), Money.Coins(0.3m)).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(0.4m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	private IEnumerable<SmartCoin> GenerateSmartCoins(IEnumerable<(string Cluster, decimal amount)> coins)
	{
		Dictionary<string, List<(HdPubKey key, decimal amount)>> generatedKeyGroup = new();

		// Create cluster-grouped keys
		foreach (var targetCoin in coins)
		{
			var key = KeyManager.GenerateNewKey(new SmartLabel(targetCoin.Cluster), KeyState.Clean, false);

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
				x.Select(y => BitcoinFactory.CreateSmartCoin(y.key, y.amount))); // Generate the final SmartCoins.
	}
}
