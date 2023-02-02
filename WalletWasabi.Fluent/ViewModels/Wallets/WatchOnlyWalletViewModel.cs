using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class WatchOnlyWalletViewModel : WalletViewModel
{
	internal WatchOnlyWalletViewModel(NavBarWalletStateViewModel parent)
		: base(parent)
	{
	}
}
