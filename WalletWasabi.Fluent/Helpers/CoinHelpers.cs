using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Helpers;

public static class CoinHelpers
{
	public static bool IsPrivate(this SmartCoin coin, int privateThreshold)
	{
		return coin.HdPubKey.AnonymitySet >= privateThreshold;
	}

	public static SmartLabel GetLabels(this SmartCoin coin, int privateThreshold)
	{
		if (coin.IsPrivate(privateThreshold))
		{
			return SmartLabel.Empty;
		}

		if (coin.HdPubKey.Cluster.Labels == SmartLabel.Empty)
		{
			return CoinPocketHelper.UnlabelledFundsText;
		}

		return coin.HdPubKey.Cluster.Labels;
	}
}
