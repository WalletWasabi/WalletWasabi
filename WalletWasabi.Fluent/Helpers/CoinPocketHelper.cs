using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Helpers;

public static class CoinPocketHelper
{
	public static readonly SmartLabel UnlabelledFundsText = new("Unknown People");
	public static readonly SmartLabel PrivateFundsText = new("Private Funds");

	public static IEnumerable<(SmartLabel SmartLabel, ICoinsView Coins)> GetPockets(this ICoinsView allCoins, int privateAnonSetThreshold)
	{
		List<(SmartLabel SmartLabel, ICoinsView Coins)> pockets = new();
		var clusters = new Dictionary<SmartLabel, List<SmartCoin>>();

		foreach (SmartCoin coin in allCoins.Where(x => x.HdPubKey.AnonymitySet < privateAnonSetThreshold))
		{
			var cluster = coin.HdPubKey.Cluster.Labels.ToString();

			if (clusters.Keys.FirstOrDefault(x => string.Equals(x, cluster, StringComparison.OrdinalIgnoreCase)) is { } key &&
			    clusters.TryGetValue(key, out var clusterCoins))
			{
				clusterCoins.Add(coin);
			}
			else
			{
				clusters.Add(cluster, new List<SmartCoin> { coin });
			}
		}

		CoinsView? unLabelledCoins = null;

		foreach (var cluster in clusters)
		{
			string[] allLabels = cluster.Key.Labels.ToArray();
			SmartCoin[] coins = cluster.Value.ToArray();

			if (allLabels.Length == 0)
			{
				unLabelledCoins = new CoinsView(coins);
			}
			else
			{
				pockets.Add(new(cluster.Key, new CoinsView(coins)));
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
