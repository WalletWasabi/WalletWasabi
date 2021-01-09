using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.WalletPages
{
	public abstract partial class WalletPageViewModel : NavBarItemViewModel
	{
		protected WalletPageViewModel(Wallet wallet)
		{
			Wallet = Guard.NotNull(nameof(wallet), wallet);
		}

		public Wallet Wallet { get; }
	}
}