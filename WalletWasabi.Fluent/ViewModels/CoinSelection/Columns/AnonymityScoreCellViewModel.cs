using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;

public class AnonymityScoreCellViewModel : ViewModelBase
{
	public AnonymityScoreCellViewModel(WalletCoinViewModel coin, int anonScoreTarget)
	{
		PrivacyScore = coin.AnonymitySet;
		PrivacyLevel = coin.Coin.GetPrivacyLevel(anonScoreTarget);
	}

	public PrivacyLevel PrivacyLevel { get; }

	public int PrivacyScore { get; }
}
