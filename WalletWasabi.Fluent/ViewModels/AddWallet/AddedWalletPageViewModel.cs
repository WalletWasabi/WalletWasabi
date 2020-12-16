using System.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class AddedWalletPageViewModel : RoutableViewModel
	{
		public AddedWalletPageViewModel(WalletManager walletManager, KeyManager keyManager, WalletType type)
		{
			Title = "Success";
			WalletName = keyManager.WalletName;
			Type = type;

			NextCommand = ReactiveCommand.Create(
				() =>
				{
					walletManager.AddWallet(keyManager);

					Navigate().Clear();

					var navBar = NavigationManager.Get<NavBarViewModel>();

					var wallet = navBar?.Items.FirstOrDefault(x => x.WalletName == WalletName);

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