using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class CoinjoinMoreSuggestionViewModel : SuggestionViewModel
{
	[AutoNotify] private bool _isCoinjoinEnabled;

	public CoinjoinMoreSuggestionViewModel(Wallet wallet)
	{
		IsCoinjoinEnabled = wallet.KeyManager.AutoCoinJoin;
	}
}
