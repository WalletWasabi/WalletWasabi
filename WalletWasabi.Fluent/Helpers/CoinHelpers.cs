using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Helpers;

public static class CoinHelpers
{
	public static bool IsPrivate(this SmartCoin coin, int privateThreshold)
	{
		return coin.HdPubKey.AnonymitySet >= privateThreshold;
	}

	public static SmartLabel GetLabels(this SmartCoin coin, int privateThreshold) => coin.IsPrivate(privateThreshold) ? SmartLabel.Empty : coin.HdPubKey.Cluster.Labels;
}
