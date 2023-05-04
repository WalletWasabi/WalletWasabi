using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models;

public class Pocket
{
	public Pocket((SmartLabel labels, ICoinsView coins) pocket)
	{
		Coins = pocket.coins;
		Labels = pocket.labels;
	}

	public SmartLabel Labels { get; }

	public Money Amount => Coins.TotalAmount();

	public ICoinsView Coins { get; }

	public static Pocket Empty => new((SmartLabel.Empty, new CoinsView(Enumerable.Empty<SmartCoin>())));

	public bool IsPrivate(int privateThreshold)
	{
		return Coins.All(x => x.IsPrivate(privateThreshold));
	}

	public bool IsSemiPrivate(int privateThreshold, int semiPrivateThreshold)
	{
		return Coins.All(x => x.IsSemiPrivate(privateThreshold, semiPrivateThreshold));
	}

	public bool IsUnknown(int semiPrivateThreshold)
	{
		var allLabel = Coins.SelectMany(x => x.HdPubKey.Cluster.Labels);
		var isAllCoinNonPrivate = Coins.All(x => x.HdPubKey.AnonymitySet < semiPrivateThreshold);
		var mergedLabels = new SmartLabel(allLabel);

		return mergedLabels.IsEmpty && isAllCoinNonPrivate;
	}

	public bool IsUnconfirmed()
	{
		return Coins.Any(x => !x.Confirmed);
	}

	public static Pocket Merge(params Pocket[] pockets)
	{
		var mergedLabels = SmartLabel.Merge(pockets.Select(p => p.Labels));
		var mergedCoins = new CoinsView(pockets.SelectMany(x => x.Coins).ToHashSet());

		return new Pocket((mergedLabels, mergedCoins));
	}

	public static Pocket Merge(Pocket[] pocketArray, params Pocket[] pockets)
	{
		var mergedPocketArray = Merge(pocketArray);
		var mergedPockets = Merge(pockets);

		return Merge(mergedPocketArray, mergedPockets);
	}
}
