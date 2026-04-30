using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.PrivacyRing;

public class PrivacyBarItemViewModel : ViewModelBase
{
	public PrivacyBarItemViewModel(UiContext uiContext, PrivacyLevel privacyLevel, decimal amount) : base(uiContext)
	{
		PrivacyLevel = privacyLevel;
		Amount = amount;
	}

	public decimal Amount { get; }

	public PrivacyLevel PrivacyLevel { get; }
}
