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

	public static IEnumerable<(SmartLabel SmartLabel, ICoinsView Coins)> GetPockets(this ICoinsView allCoins, int privateAnonSetThreshold)
	{
		List<(SmartLabel SmartLabel, ICoinsView Coins)> pockets = new();

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

	public static IEnumerable<Pocket> GetPockets(this Wallet wallet) => wallet.Coins.GetPockets(wallet.KeyManager.MinAnonScoreTarget).Select(x => new Pocket(x));
}
