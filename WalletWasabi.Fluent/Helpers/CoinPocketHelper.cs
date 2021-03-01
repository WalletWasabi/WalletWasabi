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
		public static readonly string[] UnlabelledFundsText = {"Unlabelled Funds"};
		public static readonly string[] PrivateFundsText = {"Private Funds"};

		public static IEnumerable<(string[] Labels, ICoinsView Coins)> GetPockets(this ICoinsView allCoins, int privateAnonSetThreshold)
		{
			List<(string[] Labels, ICoinsView Coins)> pockets = new();

			var clusters = allCoins
				.Where(x => x.HdPubKey.AnonymitySet < privateAnonSetThreshold)
				.GroupBy(x => x.HdPubKey.Cluster.Labels);

			CoinsView? unLabelledCoins = null;

			foreach (var cluster in clusters)
			{
				string[] allLabels = cluster.Key.Labels.ToArray();
				SmartCoin[] coins = cluster.ToArray();

				if (allLabels.Length == 0)
				{
					unLabelledCoins = new CoinsView(coins);
				}
				else
				{
					pockets.Add(new(allLabels, new CoinsView(coins)));
				}
			}

			if (unLabelledCoins is { })
			{
				pockets.Add(new(UnlabelledFundsText, unLabelledCoins));
			}

			var privateCoins = new CoinsView(allCoins.Where(x => x.HdPubKey.AnonymitySet >= privateAnonSetThreshold));
			if (privateCoins.Any())
			{
				pockets.Add(new(PrivateFundsText, privateCoins));
			}

			return pockets;
		}
	}
}
