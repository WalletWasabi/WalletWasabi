using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;

public class AnonymityScoreCellViewModel : ViewModelBase
{
	public AnonymityScoreCellViewModel(WalletCoinViewModel coin)
	{
		PrivacyScore = coin.AnonymitySet;
		PrivacyLevel = coin.GetPrivacyLevel();
	}

	public PrivacyLevel PrivacyLevel { get; }

	public int PrivacyScore { get; }
}
