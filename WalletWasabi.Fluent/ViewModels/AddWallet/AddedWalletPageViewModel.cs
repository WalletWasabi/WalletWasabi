using System.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public enum WalletType
	{
		Normal,
		Hardware,
		Coldcard,
		Trezor,
		Ledger
	}

	public class AddedWalletPageViewModel : RoutableViewModel
	{
		public AddedWalletPageViewModel(string walletName, WalletType type)
		{
			WalletName = walletName;
			Type = type;

			NextCommand = ReactiveCommand.Create(
				() =>
				{
					Navigate().Clear();

					var navBar = NavigationManager.Get<NavBarViewModel>();

					var wallet = navBar?.Items.FirstOrDefault(x => x.WalletName == walletName);

					if (wallet is { } && navBar is { })
					{
						navBar.SelectedItem = wallet;
						Navigate(NavigationTarget.HomeScreen).To(wallet, NavigationMode.Clear);
					}
				});
		}

		public WalletType Type { get; }

		public string WalletName { get; }
	}
}