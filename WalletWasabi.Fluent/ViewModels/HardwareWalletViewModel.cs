using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels
{
	public class HardwareWalletViewModel : WalletViewModel
	{
		internal HardwareWalletViewModel(IScreen screen, UiConfig uiConfig, Wallet wallet) : base(screen, uiConfig, wallet)
		{
		}
	}
}
