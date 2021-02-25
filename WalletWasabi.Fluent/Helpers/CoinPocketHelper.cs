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
		public static IEnumerable<(string Labels, ICoinsView Coins)> GetPockets(ICoinsView allCoins)
		{
			var clusters = allCoins.GroupBy(x => x.HdPubKey.Cluster.Labels);

			List<(string Labels, ICoinsView Coins)> pockets = new();

			foreach (var cluster in clusters)
			{
				string allLabels = cluster.Key.ToString();
				SmartCoin[] coins = cluster.ToArray();

				if (string.IsNullOrWhiteSpace(allLabels))
				{
					// If the Label is empty then add every coin as a separate pocket
					foreach (var coin in coins)
					{
						pockets.Add(new("", new CoinsView(new[] { coin })));
					}
				}
				else
				{
					pockets.Add(new(allLabels, new CoinsView(coins)));
				}
			}
			return pockets;
		}
	}
}
