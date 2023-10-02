using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Models;

public class Pocket
{
	public Pocket((LabelsArray labels, ICoinsView coins) pocket)
	{
		Coins = pocket.coins;
		Labels = pocket.labels;
	}

	public LabelsArray Labels { get; }

	public Money Amount => Coins.TotalAmount();

	public ICoinsView Coins { get; }

	public static Pocket Empty => new((LabelsArray.Empty, new CoinsView(Enumerable.Empty<SmartCoin>())));

	public static Pocket Merge(params Pocket[] pockets)
	{
		var mergedLabels = LabelsArray.Merge(pockets.Select(p => p.Labels));
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
