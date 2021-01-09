using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	public abstract partial class WalletActionViewModel : NavBarItemViewModel
	{
		protected WalletActionViewModel(Wallet wallet)
		{
			Wallet = Guard.NotNull(nameof(wallet), wallet);
		}

		public Wallet Wallet { get; }
	}
}