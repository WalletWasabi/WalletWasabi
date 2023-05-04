using System.Collections.Generic;
using System.Linq;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.UserInterfaceTest;
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

		SmartCoinSelector selector = new(availableCoins, recipient: "Jose", privateThreshold: 999, semiPrivateThreshold: 2);
		IEnumerable<ICoin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, Money.Coins(target));

		Coin theOnlyOne = Assert.Single(coinsToSpend.Cast<Coin>());
		Assert.Equal(target, theOnlyOne.Amount.ToUnit(MoneyUnit.BTC));
	}

	[Fact]
	public void DontSelectUnnecessaryInputs()
	{
		Money target = Money.Coins(4m);
		List<SmartCoin> availableCoins = GenerateSmartCoins(Enumerable.Range(0, 10).Select(i => ("Juan", 0.1m * (i + 1))));

		SmartCoinSelector selector = new(availableCoins, recipient: "Jose", privateThreshold: 999, semiPrivateThreshold: 2);
		List<Coin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(5, coinsToSpend.Count);
		Assert.Equal(target, Money.Satoshis(coinsToSpend.Sum(x => x.Amount)));
	}

	[Fact]
	public void PreferSameClusterOverExactAmount()
	{
		Money target = Money.Coins(0.3m);
		List<SmartCoin> availableCoins = GenerateSmartCoins(("Besos", 0.2m), ("Besos", 0.2m), ("Juan", 0.1m), ("Juan", 0.1m));

		SmartCoinSelector selector = new(availableCoins, recipient: "Jose", privateThreshold: 999, semiPrivateThreshold: 2);
		List<Coin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		// We do NOT expect an exact match, because that would mix the clusters.
		Assert.Equal(Money.Coins(0.4m), Money.Satoshis(coinsToSpend.Sum(x => x.Amount)));
	}

	[Fact]
	public void PreferExactAmountWhenClustersAreDifferent()
	{
		Money target = Money.Coins(0.3m);
		List<SmartCoin> availableCoins = GenerateSmartCoins(("Besos", 0.2m), ("Juan", 0.1m), ("Adam", 0.2m), ("Eve", 0.1m));

		SmartCoinSelector selector = new(availableCoins, recipient: "Jose", privateThreshold: 999, semiPrivateThreshold: 2);
		List<Coin> coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(target, Money.Satoshis(coinsToSpend.Sum(x => x.Amount)));       // Cluster-privacy is indifferent, so aim for exact amount.
	}

	[Fact]
	public void DontUseTheWholeClusterIfNotNecessary()
	{
		Money target = Money.Coins(0.3m);
		List<SmartCoin> availableCoins = GenerateDuplicateSmartCoins(("Juan", 0.1m), count: 10);

		SmartCoinSelector selector = new(availableCoins, recipient: "Jose", privateThreshold: 999, semiPrivateThreshold: 2);
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

		SmartCoinSelector selector = new(availableCoins, recipient: "Jose", privateThreshold: 999, semiPrivateThreshold: 2);
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

		var selector = new SmartCoinSelector(smartCoins, recipient: "Jose", privateThreshold: 999, semiPrivateThreshold: 2);
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

		var selector = new SmartCoinSelector(smartCoins, recipient: "Jose", privateThreshold: 999, semiPrivateThreshold: 2);
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

		var selector = new SmartCoinSelector(coinsKnownByJuan.Concat(coinsKnownByBeto).ToList(), recipient: "Jose", privateThreshold: 999, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(0.4m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Fact]
	public void PreferPrivatePocket()
	{
		Money target = Money.Coins(0.3m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.4m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.5m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.6m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.7m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: "Jose", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Single(coinsToSpend);
		Assert.Equal(0.4m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Fact]
	public void PreferSemiPrivatePocket()
	{
		Money target = Money.Coins(0.5m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.4m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.5m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.6m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.7m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: "Jose", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Single(coinsToSpend);
		Assert.Equal(0.5m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Fact]
	public void PreferPrivateAndSemiPrivatePocket()
	{
		Money target = Money.Coins(0.9m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.4m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.5m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.6m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.7m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: "Jose", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Equal(0.9m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Fact]
	public void AvoidUnlabelledPocket()
	{
		Money target = Money.Coins(0.7m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.3m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.7m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: "Jose", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(3, coinsToSpend.Count);
		Assert.Equal(0.7m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Fact]
	public void PreferPocketKnownByRecipient()
	{
		Money target = Money.Coins(0.7m);

		var knownByJoseCoin = LabelTestExtensions.CreateCoin(0.3m, "Jose", anonymitySet: 1);
		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.3m, "Lucas", anonymitySet: 1),
			knownByJoseCoin,
			LabelTestExtensions.CreateCoin(0.7m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: "Jose", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(3, coinsToSpend.Count);
		Assert.Equal(0.7m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
		Assert.Contains(knownByJoseCoin.Coin, coinsToSpend);
	}

	[Fact]
	public void PreferUnlabelledPocketWhenKnownIsUnnecessary()
	{
		Money target = Money.Coins(0.7m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.2m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: "Jose", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(3, coinsToSpend.Count);
		Assert.Equal(0.7m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Fact]
	public void UseAllPockets()
	{
		Money target = Money.Coins(0.7m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.2m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.1m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: "Jose", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(4, coinsToSpend.Count);
		Assert.Equal(0.7m, coinsToSpend.Sum(x => x.Amount.ToUnit(MoneyUnit.BTC)));
	}

	[Theory]
	[InlineData("Jose, Lucas", "Jose, Lucas")]
	[InlineData("jOSE, lUCAS", "Jose, Lucas")]
	[InlineData("Jose, Lucas", "jOSE, lUCAS")]
	public void SelectOnlyKnownByRecipientPocketTests(string label, string recipient)
	{
		Money target = Money.Coins(0.1m);

		var knownByJoseLucasCoin = LabelTestExtensions.CreateCoin(0.5m, label, anonymitySet: 1);
		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.3m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Jose", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Jose, Lucas, Federico", anonymitySet: 1),
			knownByJoseLucasCoin,
			LabelTestExtensions.CreateCoin(0.7m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: recipient, privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Single(coinsToSpend);
		Assert.Contains(knownByJoseLucasCoin.Coin, coinsToSpend);
	}

	[Theory]
	[InlineData("David, Lucas", "David")]
	[InlineData("David, Lucas, Jose", "David, Lucas")]
	public void SelectKnownByRecipientPocketTests(string label, string recipient)
	{
		Money target = Money.Coins(0.41m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.1m, label, anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Jose", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Jose, Lucas, Federico", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.7m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: recipient, privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(3, coinsToSpend.Count);
		Assert.Contains(coins[0].Coin, coinsToSpend);
		Assert.Contains(coins[1].Coin, coinsToSpend);
		Assert.Contains(coins[2].Coin, coinsToSpend);
	}

	[Fact]
	public void PreferKnownByRecipientPocket()
	{
		Money target = Money.Coins(0.71m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.1m, "David, Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Jose", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Jose, Lucas, Federico", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.7m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: "Lucas", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(4, coinsToSpend.Count);
		Assert.Contains(coins[0].Coin, coinsToSpend);
		Assert.Contains(coins[1].Coin, coinsToSpend);
		Assert.Contains(coins[2].Coin, coinsToSpend);
		Assert.Contains(coins[3].Coin, coinsToSpend);
	}

	[Fact]
	public void OnlySelectNecessaryKnownByRecipientPocket()
	{
		Money target = Money.Coins(1m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 10), // Private pocket
			LabelTestExtensions.CreateCoin(0.2m, "", anonymitySet: 4), // Semi-private pocket
			LabelTestExtensions.CreateCoin(0.3m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.1m, "Lucas, David", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Lucas, David, Jose", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.3m, "Jose, Lucas, Federico", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(0.7m, "", anonymitySet: 1), // Unlabelled pocket
		};

		var selector = new SmartCoinSelector(coins, recipient: "Lucas", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(4, coinsToSpend.Count);
		Assert.Contains(coins[0].Coin, coinsToSpend);
		Assert.Contains(coins[1].Coin, coinsToSpend);
		Assert.Contains(coins[2].Coin, coinsToSpend);
		Assert.Contains(coins[4].Coin, coinsToSpend);
	}

	[Fact]
	public void PreferConfirmedPocket()
	{
		Money target = Money.Coins(1m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(1.1m, "Lucas", anonymitySet: 1),
			LabelTestExtensions.CreateCoin(1m, "Jose", anonymitySet: 1, isConfirmed: false),
		};

		var selector = new SmartCoinSelector(coins, recipient: "David", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Single(coinsToSpend);
		Assert.Contains(coins[0].Coin, coinsToSpend);
	}

	[Fact]
	public void AvoidUnconfirmedPrivateCoins()
	{
		Money target = Money.Coins(1m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.5m, anonymitySet: 999),
			LabelTestExtensions.CreateCoin(0.5m, anonymitySet: 999),
			LabelTestExtensions.CreateCoin(1m, anonymitySet: 999, isConfirmed: false),
			LabelTestExtensions.CreateCoin(1m, anonymitySet: 999, isConfirmed: false),
			LabelTestExtensions.CreateCoin(1m, anonymitySet: 999, isConfirmed: false),
		};

		var selector = new SmartCoinSelector(coins, recipient: "David", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Contains(coins[0].Coin, coinsToSpend);
		Assert.Contains(coins[1].Coin, coinsToSpend);
	}

	[Fact]
	public void AvoidUnconfirmedSemiPrivateCoins()
	{
		Money target = Money.Coins(1m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.5m, anonymitySet: 4),
			LabelTestExtensions.CreateCoin(0.5m, anonymitySet: 4),
			LabelTestExtensions.CreateCoin(1m, anonymitySet: 4, isConfirmed: false),
			LabelTestExtensions.CreateCoin(1m, anonymitySet: 4, isConfirmed: false),
			LabelTestExtensions.CreateCoin(1m, anonymitySet: 4, isConfirmed: false),
		};

		var selector = new SmartCoinSelector(coins, recipient: "David", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(2, coinsToSpend.Count);
		Assert.Contains(coins[0].Coin, coinsToSpend);
		Assert.Contains(coins[1].Coin, coinsToSpend);
	}

	[Fact]
	public void IncludeUnconfirmedCoins()
	{
		Money target = Money.Coins(1m);

		var coins = new List<SmartCoin>
		{
			LabelTestExtensions.CreateCoin(0.25m, anonymitySet: 999, isConfirmed: false),
			LabelTestExtensions.CreateCoin(0.25m, anonymitySet: 4, isConfirmed: false),
			LabelTestExtensions.CreateCoin(0.25m, "Lucas", anonymitySet: 1, isConfirmed: false),
			LabelTestExtensions.CreateCoin(0.25m, "Jose", anonymitySet: 1, isConfirmed: false),
		};

		var selector = new SmartCoinSelector(coins, recipient: "David", privateThreshold: 5, semiPrivateThreshold: 2);
		var coinsToSpend = selector.Select(suggestion: EmptySuggestion, target).Cast<Coin>().ToList();

		Assert.Equal(4, coinsToSpend.Count);
		Assert.Contains(coins[0].Coin, coinsToSpend);
		Assert.Contains(coins[1].Coin, coinsToSpend);
		Assert.Contains(coins[2].Coin, coinsToSpend);
		Assert.Contains(coins[3].Coin, coinsToSpend);
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
				x.Select(y => BitcoinFactory.CreateSmartCoin(y.key, y.amount)))
			.ToList(); // Generate the final SmartCoins.
	}
}
