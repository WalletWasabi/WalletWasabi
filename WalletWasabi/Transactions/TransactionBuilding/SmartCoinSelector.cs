using NBitcoin;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Coins;
using WalletWasabi.BlockchainAnalysis.Clustering;

namespace WalletWasabi.Transactions.TransactionBuilding
{
	public class SmartCoinSelector : ICoinSelector
	{
		private IEnumerable<SmartCoin> UnspentCoins { get; }

		public SmartCoinSelector(IEnumerable<SmartCoin> unspentCoins)
		{
			UnspentCoins = Guard.NotNull(nameof(unspentCoins), unspentCoins).Distinct();
		}

		public IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target)
		{
			Money targetMoney = target as Money;

			long available = UnspentCoins.Sum(x => x.Amount);
			if (available < targetMoney)
			{
				throw new InsufficientBalanceException(targetMoney, available);
			}

			// Get unique clusters.
			IEnumerable<Cluster> uniqueClusters = UnspentCoins
				.Select(coin => coin.Clusters)
				.Distinct();

			// Build all the possible coin clusters, except when it's computationally too expensive.
			List<IEnumerable<SmartCoin>> coinClusters = (uniqueClusters.Count() < 10)
				? uniqueClusters
					.CombinationsWithoutRepetition(ofLength: 1, upToLength: 6)
					.Select(clusterCombination => UnspentCoins.Where(coin => clusterCombination.Contains(coin.Clusters)))
					.ToList()
				: new List<IEnumerable<SmartCoin>>();

			coinClusters.Add(UnspentCoins);

			// This operation is doing super advanced grouping on the coin clusters and adding properties to each of them.
			var sayajinCoinClusters = coinClusters
				.Select(coins => (Coins: coins, Privacy: 1.0m / (1 + coins.Sum(x => x.Clusters.Labels.Count()))))
				.Select(group => new
				{
					group.Coins,
					Unconfirmed = group.Coins.Any(x => !x.Confirmed),    // If group has an unconfirmed, then the whole group is unconfirmed.
					AnonymitySet = group.Coins.Min(x => x.AnonymitySet), // The group is as anonymous as its weakest member.
					ClusterPrivacy = group.Privacy, // The number people/entities that know the cluster.
					Amount = group.Coins.Sum(x => x.Amount)
				});

			// Find the best coin cluster that we are going to use.
			IEnumerable<SmartCoin> bestCoinCluster = sayajinCoinClusters
				.Where(group => group.Amount >= targetMoney)
				.OrderBy(group => group.Unconfirmed)
				.ThenByDescending(group => group.AnonymitySet)     // Always try to spend/merge the largest anonset coins first.
				.ThenByDescending(group => group.ClusterPrivacy)   // Select lesser-known coins.
				.ThenByDescending(group => group.Amount)           // Then always try to spend by amount.
				.First()
				.Coins;

			var coinsInBestClusterByScript = bestCoinCluster
				.GroupBy(c => c.ScriptPubKey)
				.Select(group => new
				{
					Coins = group
				});

			List<SmartCoin> coinsToSpend = new List<SmartCoin>();

			foreach (IGrouping<Script, SmartCoin> coinsGroup in coinsInBestClusterByScript
				.Select(group => group.Coins))
			{
				coinsToSpend.AddRange(coinsGroup);

				if (coinsToSpend.Sum(x => x.Amount) >= targetMoney)
				{
					break;
				}
			}

			return coinsToSpend.Select(c => c.GetCoin());
		}
	}
}
