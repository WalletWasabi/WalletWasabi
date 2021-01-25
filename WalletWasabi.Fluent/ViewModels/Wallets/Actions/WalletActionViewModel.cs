using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Actions
{
	public abstract partial class WalletActionViewModel : NavBarItemViewModel
	{
		[AutoNotify] private bool _isEnabled;

		protected WalletActionViewModel(WalletViewModelBase wallet)
		{
			Wallet = Guard.NotNull(nameof(wallet), wallet);
		}

		public WalletViewModelBase Wallet { get; }

		public override string ToString() => Title;
	}
}