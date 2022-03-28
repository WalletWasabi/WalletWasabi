using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

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
}
