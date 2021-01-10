using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	public abstract partial class WalletActionViewModel : NavBarItemViewModel
	{
		protected WalletActionViewModel(WalletViewModelBase wallet)
		{
			Wallet = Guard.NotNull(nameof(wallet), wallet);
		}

		public WalletViewModelBase Wallet { get; }
	}
}