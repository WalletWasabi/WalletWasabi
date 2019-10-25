using NBitcoin;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Coins;
using WalletWasabi.BlockchainAnalysis;

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
			var targetMoney = target as Money;

			var available = UnspentCoins.Sum(x => x.Amount);
			if (available < targetMoney)
			{
				throw new InsufficientBalanceException(targetMoney, available);
			}

			var uniqueClusters = UnspentCoins
				.Select(coin => coin.Clusters)
				.Distinct();

			var clusters = (uniqueClusters.Count() < 10)
				? uniqueClusters
					.CombinationsWithoutRepetition(ofLength: 1, upToLength: 6)
					.Select(clusterCombination => UnspentCoins.Where(coin => clusterCombination.Contains(coin.Clusters)))
					.ToList()
				: new List<IEnumerable<SmartCoin>>();

			clusters.Add(UnspentCoins);

			var coinsByCluster = clusters
				.Select(coins => (Coins: coins, Privacy: 1.0m / coins.SelectMany(x => x.Clusters.KnownBy).Count()))
				.Select(group => new
				{
					group.Coins,
					Unconfirmed = group.Coins.Any(x => !x.Confirmed),    // If group has an unconfirmed, then the whole group is unconfirmed.
					AnonymitySet = group.Coins.Min(x => x.AnonymitySet), // The group is as anonymous as its weakest member.
					ClusterPrivacy = group.Privacy, // The number people/entities that know the cluster.
					Amount = group.Coins.Sum(x => x.Amount)
				});

			var coinsInBestCluster = coinsByCluster
				.Where(group => group.Amount >= targetMoney)
				.OrderBy(group => group.Unconfirmed)
				.ThenByDescending(group => group.AnonymitySet)     // Always try to spend/merge the largest anonset coins first.
				.ThenByDescending(group => group.ClusterPrivacy)   // Select lesser-known coins.
				.ThenByDescending(group => group.Amount)           // Then always try to spend by amount.
				.First()
				.Coins;

			var coinsInBestClusterByScript = coinsInBestCluster
				.GroupBy(c => c.ScriptPubKey)
				.Select(group => new
				{
					Coins = group,
					Unconfirmed = group.Any(x => !x.Confirmed),    // If group has an unconfirmed, then the whole group is unconfirmed.
					AnonymitySet = group.Min(x => x.AnonymitySet), // The group is as anonymous as its weakest member.
					ClusterPrivacy = 1.0 / group.First().Clusters.KnownBy.Count(), // The number people/entities that know the cluster.
					ClusterSize = group.First().Clusters.Size,    // The number of coins in the cluster.
					Amount = group.Sum(x => x.Amount)
				});

			var coinsToSpend = new List<SmartCoin>();

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
