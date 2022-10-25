using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

namespace WalletWasabi.Fluent.Helpers;

internal static class PrivacyLevelHelper
{
	public static string GetLabelFromPrivacyLevel(PrivacyLevel privacyLevel)
	{
		return privacyLevel switch
		{
			PrivacyLevel.None => "(Invalid privacy level)",
			PrivacyLevel.SemiPrivate => CoinPocketHelper.SemiPrivateFundsText,
			PrivacyLevel.Private => CoinPocketHelper.PrivateFundsText,
			PrivacyLevel.NonPrivate => CoinPocketHelper.UnlabelledFundsText,
			_ => throw new ArgumentOutOfRangeException(nameof(privacyLevel), privacyLevel, null)
		};
	}
}
