using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class CoinPocketHelper
{
	public static readonly SmartLabel UnlabelledFundsText = new("Unknown People");
	public static readonly SmartLabel PrivateFundsText = new("Private Funds");
	public static readonly SmartLabel SemiPrivateFundsText = new("Semi-Private Funds");

	public static IEnumerable<(SmartLabel SmartLabel, ICoinsView Coins)> GetPockets(this ICoinsView allCoins, int privateAnonSetThreshold)
	{
		List<(SmartLabel SmartLabel, ICoinsView Coins)> pockets = new();
		var clusters = new Dictionary<SmartLabel, List<SmartCoin>>();

		foreach (SmartCoin coin in allCoins.Where(x => x.HdPubKey.AnonymitySet < 2))
		{
			var cluster = coin.HdPubKey.Cluster.Labels;

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

		var semiPrivateCoins = new CoinsView(allCoins.Where(x => x.HdPubKey.AnonymitySet >= 2 && x.HdPubKey.AnonymitySet < privateAnonSetThreshold));
		if (semiPrivateCoins.Any())
		{
			pockets.Add(new(SemiPrivateFundsText, semiPrivateCoins));
		}

		return pockets;
	}

	public static IEnumerable<Pocket> GetPockets(this Wallet wallet) => wallet.Coins.GetPockets(wallet.KeyManager.AnonScoreTarget).Select(x => new Pocket(x));
}
