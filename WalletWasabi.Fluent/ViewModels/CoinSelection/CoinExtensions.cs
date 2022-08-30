using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public static class CoinExtensions
{
	public static PrivacyLevel GetPrivacyLevel(this SmartCoin coin, int anonScoreTarget)
	{
		if (coin.IsPrivate(anonScoreTarget))
		{
			return PrivacyLevel.Private;
		}

		if (coin.IsSemiPrivate())
		{
			return PrivacyLevel.SemiPrivate;
		}

		return PrivacyLevel.NonPrivate;
	}
}
