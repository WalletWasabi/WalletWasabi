using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Helpers;

public static class CoinHelpers
{
	extension(SmartCoin coin)
	{
		public bool IsPrivate(int privateThreshold) => coin.AnonymitySet >= privateThreshold;

		public bool IsSemiPrivate(int privateThreshold, int semiPrivateThreshold = Constants.SemiPrivateThreshold) => coin.AnonymitySet >= semiPrivateThreshold && coin.AnonymitySet < privateThreshold;

		public bool IsRedCoin(int semiPrivateThreshold = Constants.SemiPrivateThreshold) => coin.AnonymitySet < semiPrivateThreshold;
	}
}
