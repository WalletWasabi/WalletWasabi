using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

namespace WalletWasabi.Fluent.Helpers;

internal static class PrivacyLevelHelper
{
	public static string GetLabelFromPrivacyLevel(PrivacyLevel privacyLevel)
	{
		return privacyLevel switch
		{
			PrivacyLevel.None => "(Invalid privacy level)",
			PrivacyLevel.SemiPrivate => "Semi-private",
			PrivacyLevel.Private => "Private",
			PrivacyLevel.NonPrivate => "Unknown People",
			_ => throw new ArgumentOutOfRangeException(nameof(privacyLevel), privacyLevel, null)
		};
	}
}
