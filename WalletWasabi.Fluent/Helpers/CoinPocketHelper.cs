using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Helpers
{
	public static class CoinPocketHelper
	{
		public static IEnumerable<(string Labels, ICoinsView Coins)> GetPockets(this ICoinsView allCoins, int anonymitySet)
		{
			var clusters = allCoins.Where(x=>x.HdPubKey.AnonymitySet < anonymitySet)
				.GroupBy(x => x.HdPubKey.Cluster.Labels);

			List<(string Labels, ICoinsView Coins)> pockets = new();

			foreach (var cluster in clusters)
			{
				string allLabels = cluster.Key.ToString();
				SmartCoin[] coins = cluster.ToArray();

				if (string.IsNullOrWhiteSpace(allLabels))
				{
					// If the Label is empty then add every coin as a separate pocket
					pockets.Add(new("Unlabelled Funds", new CoinsView(coins)));
				}
				else
				{
					pockets.Add(new(allLabels, new CoinsView(coins)));
				}
			}

			pockets.Add(new("Private Funds", new CoinsView(allCoins.Where(x=>x.HdPubKey.AnonymitySet >= anonymitySet))));

			return pockets;
		}
	}
}
