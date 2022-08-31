using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public static class CoinExtensions
{
	public static PrivacyLevel GetPrivacyLevel(this WalletCoinViewModel coinViewModel)
	{
		var anonScoreTarget = coinViewModel.Wallet.AnonScoreTarget;

		if (coinViewModel.Coin.IsPrivate(anonScoreTarget))
		{
			return PrivacyLevel.Private;
		}

		if (coinViewModel.Coin.IsSemiPrivate())
		{
			return PrivacyLevel.SemiPrivate;
		}

		return PrivacyLevel.NonPrivate;
	}
}
