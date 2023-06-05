using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public class WatchOnlyWalletViewModel : WalletViewModel
{
	internal WatchOnlyWalletViewModel(UiContext uiContext, WalletPageViewModel parent)
		: base(uiContext, parent)
	{
	}
}
