using NBitcoin;
using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Exceptions;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using System.Collections.Immutable;
using WalletWasabi.Extensions;

namespace WalletWasabi.Blockchain.TransactionBuilding;

public class SmartCoinSelector : ICoinSelector
{
	public SmartCoinSelector(List<SmartCoin> unspentCoins)
	{
		_unspentCoins = unspentCoins.Distinct().ToList();
	}

	private readonly List<SmartCoin> _unspentCoins;
	private int IterationCount { get; set; }
	private Exception? LastTransactionSizeException { get; set; }

	/// <param name="suggestion">We use this to detect if NBitcoin tries to suggest something different and indicate the error.</param>
	/// <param name="target">Only <see cref="Money"/> type is really supported by this implementation.</param>
	/// <remarks>Do not call this method repeatedly on a single <see cref="SmartCoinSelector"/> instance.</remarks>
	public IEnumerable<ICoin> Select(IEnumerable<ICoin> suggestion, IMoney target)
	{
		var targetMoney = (Money)target;

		long available = _unspentCoins.Sum(x => x.Amount);
		if (available < targetMoney)
		{
			throw new InsufficientBalanceException(targetMoney, available);
		}

		if (IterationCount > 500)
		{
			if (LastTransactionSizeException is not null)
			{
				throw LastTransactionSizeException;
			}

			throw new TimeoutException("Coin selection timed out.");
		}

		// The first iteration should never take suggested coins into account .
		if (IterationCount > 0)
		{
			Money suggestedSum = Money.Satoshis(suggestion.Sum(c => (Money)c.Amount));
			if (suggestedSum < targetMoney)
			{
				LastTransactionSizeException = new TransactionSizeException(targetMoney, suggestedSum);
			}
		}

		// Get unique clusters.
		IEnumerable<Cluster> uniqueClusters = _unspentCoins
			.Select(coin => coin.HdPubKey.Cluster)
			.Distinct();

		// Build all the possible coin clusters, except when it's computationally too expensive.
		List<List<SmartCoin>> coinClusters = uniqueClusters.Count() < 10
			? uniqueClusters
				.CombinationsWithoutRepetition(ofLength: 1, upToLength: 6)
				.Select(clusterCombination => _unspentCoins
					.Where(coin => clusterCombination.Contains(coin.HdPubKey.Cluster))
					.ToList())
				.ToList()
			: new List<List<SmartCoin>>();

		coinClusters.Add(_unspentCoins);

		// This operation is doing super advanced grouping on the coin clusters and adding properties to each of them.
		var sayajinCoinClusters = coinClusters
			.Select(coins => (Coins: coins, Privacy: 1.0m / (1 + coins.Sum(x => x.HdPubKey.ClusterLabels.Count))))
			.Select(group => (
				Coins: group.Coins,
				Unconfirmed: group.Coins.Any(x => !x.Confirmed),    // If group has an unconfirmed, then the whole group is unconfirmed.
				AnonymitySet: group.Coins.Min(x => x.HdPubKey.AnonymitySet), // The group is as anonymous as its weakest member.
				ClusterPrivacy: group.Privacy, // The number people/entities that know the cluster.
				Amount: group.Coins.Sum(x => x.Amount)
			));

		// Find the best coin cluster that we are going to use.
		IEnumerable<SmartCoin> bestCoinCluster = sayajinCoinClusters
			.Where(group => group.Amount >= targetMoney)
			.OrderBy(group => group.Unconfirmed)
			.ThenByDescending(group => group.AnonymitySet)     // Always try to spend/merge the largest anonset coins first.
			.ThenByDescending(group => group.ClusterPrivacy)   // Select lesser-known coins.
			.ThenBy(group => group.Amount)           // Once we order them by cluster-privacy, we want to be as close to the target as we can.
			.First()
			.Coins;

		var coinsInBestClusterByScript = bestCoinCluster
			.GroupBy(c => c.ScriptPubKey)
			.Select(group => (ScriptPubKey: group.Key, Coins: group.ToList()))
			.OrderByDescending(x => x.Coins.Sum(c => c.Amount))
			.ToImmutableList();

		// {1} {2} ... {n} {1, 2} {1, 2, 3} {1, 2, 3, 4} ... {1, 2, 3, 4, 5 ... n}
		var coinsGroup = coinsInBestClusterByScript.Select(x => ImmutableList.Create(x))
				.Concat(coinsInBestClusterByScript.Scan(ImmutableList<(Script ScriptPubKey, List<SmartCoin> Coins)>.Empty, (acc, coinGroup) => acc.Add(coinGroup)));

		// Flattens the groups of coins and filters out the ones that are too small.
		// Finally it sorts the solutions by amount and coins (those with less coins on the top).
		var candidates = coinsGroup
			.Select(x => x.SelectMany(y => y.Coins))
			.Select(x => (Coins: x, Total: x.Sum(y => y.Amount)))
			.Where(x => x.Total >= targetMoney) // filter combinations below target
			.OrderBy(x => x.Total)              // the closer we are to the target the better
			.ThenBy(x => x.Coins.Count());      // prefer lesser coin selection on the same amount

		IterationCount++;

		// Select the best solution.
		return candidates.First().Coins.Select(x => x.Coin);
	}
}
