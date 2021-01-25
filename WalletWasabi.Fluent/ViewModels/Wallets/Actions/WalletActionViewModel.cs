using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
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

			wallet.WhenAnyValue(x => x.WalletState).Select(x => IsEnabled = (x == WalletState.Started));
		}

		public WalletViewModelBase Wallet { get; }

		public override string ToString() => Title;
	}
}