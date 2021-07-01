using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.Helpers
{
	public static class CoinHelpers
	{
		public static bool IsPrivate(this SmartCoin coin)
		{
			var privateThreshold = Services.Config.MixUntilAnonymitySetValue;
			return coin.HdPubKey.AnonymitySet >= privateThreshold;
		}

		public static SmartLabel GetLabels(this SmartCoin coin) => coin.IsPrivate() ? SmartLabel.Empty : coin.HdPubKey.Cluster.Labels;
	}
}
