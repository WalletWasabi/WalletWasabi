using System;
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
		public AddedWalletPageViewModel(WalletManager walletManager, KeyManager keyManager)
		{
			KeyManager = keyManager;
			Title = "Success";
			WalletName = keyManager.WalletName;

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

		public KeyManager KeyManager { get; }

		public string WalletName { get; }
	}
}
