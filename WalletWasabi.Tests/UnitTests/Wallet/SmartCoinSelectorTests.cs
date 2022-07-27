using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine.ClientProtocol;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class SmartCoinSelectorTests
{
	private readonly ITestOutputHelper output;

	public SmartCoinSelectorTests(ITestOutputHelper output)
	{
		this.output = output;
		KeyManager = KeyManager.Recover(new Mnemonic("all all all all all all all all all all all all"), "",
			Network.Main, KeyManager.GetAccountKeyPath(Network.Main));
	}

	private KeyManager KeyManager { get; }

	[Fact]
	public void SelectsOnlyOneCoinWhenPossible()
	{
		var smartCoins0 = GenerateSmartCoins(
			Enumerable.Range(0, 9).Select(i => ("Juan", 0.1m * (i + 1)))
		).ToList();

		var selector = new SmartCoinSelector(smartCoins0);
		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), Money.Coins(0.3m));

		var theOnlyOne = Assert.Single(coinsToSpend.Cast<Coin>());
		Assert.Equal(0.3m, theOnlyOne.Amount.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void TestUIH()
	{
		var smartCoins = GenerateSmartCoins(
			new List<(string Cluster, decimal amount)>()
			{
				("Alice", 0.0001m),
				("Alice", 0.02097152m),
				("Alice", 2.58280326m),
				("Alice", 0.00531441m),
				("Alice", 0.03188646m),
				("Alice", 0.0001m),
				("Alice", 0.001m),
				("Alice", 0.002m),
				("Alice", 0.02097152m),
				("Alice", 0.1m),
				("Alice", 0.05m),
				("Alice", 0.00016384m),
				("Alice", 0.002m),
				("Alice", 0.00039366m),
				("Alice", 0.0001m),
				("Alice", 0.00531441m),
				("Alice", 0.01062882m),
				("Alice", 0.00354294m),
				("Alice", 0.00013122m),
				("Alice", 0.001m),
				("Alice", 0.00016384m),
				("Alice", 0.02097152m),
				("Alice", 0.00016384m),
				("Alice", 0.2m),
				("Alice", 1m),
				("Alice", 0.002m),
				("Alice", 1m),
			}
		).ToList();


		output.WriteLine($"SmartCointSelector Input:");

		foreach (var coin in smartCoins)
		{
			output.WriteLine($"Coin Amount: {coin.Amount}");
		}

		output.WriteLine($"Input Sum {smartCoins.Sum(x=>x.Amount.ToDecimal(MoneyUnit.BTC))} BTC\n");

		var changeThreshold = 1.06135855m;

		output.WriteLine($"Inputs that sums up to {changeThreshold}:");

		var sum = 0m;

		foreach (var coin in smartCoins.OrderBy(x=>x.Amount))
		{
			var btcAmount = coin.Amount.ToDecimal(MoneyUnit.BTC);
			if (sum + btcAmount > changeThreshold)
			{
				break;
			}

			sum += btcAmount;
			output.WriteLine($"Coin Amount: {coin.Amount}");
		}

		var selector = new SmartCoinSelector(smartCoins);
		var coinsToSpend = selector.Select(Enumerable.Empty<Coin>(), Money.Coins(4m)).ToList();

		output.WriteLine($"\nSmartCointSelector Output:");

		foreach (var coin in coinsToSpend)
		{
			output.WriteLine($"Coin Amount: {coin.Amount}");
		}

		output.WriteLine($"Output Sum {coinsToSpend.Select(x=>x.Amount).Cast<Money>().Sum(x=>x.ToDecimal(MoneyUnit.BTC))} BTC");
	}

	[Fact]
	public void PreferLessCoinsOverExactAmount()
	{
		var smartCoins = GenerateSmartCoins(
			Enumerable.Range(0, 10).Select(i => ("Juan", 0.1m * (i + 1)))
		).ToList();

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

		var coinsKnownByBeto = GenerateSmartCoins(Enumerable.Repeat(("Juan", 0.2m), 2));

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

		return generatedKeyGroup.GroupBy(x => x.Key)
			.Select(x => x.Select(y => y.Value)) // Group the coin pairs into clusters.
			.SelectMany(x => x
				.Select(coinPair => (coinPair,
					cluster: new Cluster(coinPair.Select(z => z.key)))))
			.ForEach(x => x.coinPair.ForEach(y =>
			{
				y.key.Cluster = x.cluster;
			})) // Set each key with its corresponding cluster object.
			.Select(x => x.coinPair)
			.SelectMany(x =>
				x.Select(y => BitcoinFactory.CreateSmartCoin(y.key, y.amount))); // Generate the final SmartCoins.
	}
}
