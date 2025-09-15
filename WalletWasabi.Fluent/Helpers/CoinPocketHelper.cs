using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers;

public static class CoinPocketHelper
{
	public static readonly LabelsArray UnlabelledFundsText = new("Unknown People");
	public static readonly LabelsArray PrivateFundsText = new("Private Coins");
	public static readonly LabelsArray SemiPrivateFundsText = new("Semi-private Coins");

	public static IEnumerable<(LabelsArray Labels, ICoinsView Coins)> GetPockets(this ICoinsView allCoins, int privateAnonSetThreshold)
	{
		List<(LabelsArray Labels, ICoinsView Coins)> pockets = new();

		var clusters = new Dictionary<LabelsArray, List<SmartCoin>>(comparer: LabelsComparer.Instance);

		foreach (SmartCoin coin in allCoins.Where(x => x.IsRedCoin()))
		{
			var cluster = coin.HdPubKey.ClusterLabels;

			if (clusters.TryGetValue(cluster, out var clusterCoins))
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
			var allLabels = cluster.Key;
			var coins = cluster.Value;

			if (allLabels.IsEmpty)
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

		var privateCoins = new CoinsView(allCoins.Where(x => x.IsPrivate(privateAnonSetThreshold)));
		if (privateCoins.Any())
		{
			pockets.Add(new(PrivateFundsText, privateCoins));
		}

		var semiPrivateCoins = new CoinsView(allCoins.Where(x => x.IsSemiPrivate(privateAnonSetThreshold)));
		if (semiPrivateCoins.Any())
		{
			pockets.Add(new(SemiPrivateFundsText, semiPrivateCoins));
		}

		return pockets;
	}

	public static IEnumerable<Pocket> GetPockets(this Wallet wallet) => wallet.Coins.GetPockets(wallet.AnonScoreTarget).Select(x => new Pocket(x));
}
