using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;

namespace WalletWasabi.Fluent.Models;

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

	public Money EffectiveSumValue(FeeRate feeRate) => Coins.Sum(coin => coin.EffectiveValue(feeRate));

	public static Pocket operator +(Pocket x, Pocket y)
	{
		var mergedLabels = SmartLabel.Merge(x.Labels, y.Labels);
		var mergedCoins = new CoinsView(x.Coins.Concat(y.Coins).ToHashSet());

		return new Pocket((mergedLabels, mergedCoins));
	}

	public static Pocket operator +(Pocket[] pockets, Pocket p) => p + pockets.Aggregate((current, pocket) => current + pocket);
}
