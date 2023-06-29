using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Helpers;

public static class CoinHelpers
{
	public static bool IsPrivate<TCoin>(this TCoin coin, int privateThreshold)
		where TCoin : class, ISmartCoin, IEquatable<TCoin>
	{
		return coin.IsSufficientlyDistancedFromExternalKeys && coin.AnonymitySet >= privateThreshold;
	}

	public static bool IsSemiPrivate<TCoin>(this TCoin coin, int privateThreshold, int semiPrivateThreshold = Constants.SemiPrivateThreshold)
		where TCoin : class, ISmartCoin, IEquatable<TCoin>
	{
		return !IsRedCoin(coin, semiPrivateThreshold) && !IsPrivate(coin, privateThreshold);
	}

	public static bool IsRedCoin<TCoin>(this TCoin coin, int semiPrivateThreshold = Constants.SemiPrivateThreshold)
		where TCoin : class, ISmartCoin, IEquatable<TCoin>
	{
		return coin.AnonymitySet < semiPrivateThreshold;
	}
}
