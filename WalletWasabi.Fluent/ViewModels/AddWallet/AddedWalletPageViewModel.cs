using System;
using System.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(Title = "Success")]
	public partial class AddedWalletPageViewModel : RoutableViewModel
	{
		public AddedWalletPageViewModel(WalletManager walletManager, KeyManager keyManager)
		{
			KeyManager = keyManager;
			WalletName = keyManager.WalletName;

			NextCommand = ReactiveCommand.Create(
				() =>
				{
					walletManager.AddWallet(keyManager);

					Navigate().Clear();

					var navBar = NavigationManager.Get<NavBarViewModel>();

					var wallet = navBar?.Items.OfType<WalletViewModelBase>().FirstOrDefault(x => x.WalletName == WalletName);

					if (wallet is { } && navBar is { })
					{
						navBar.SelectedItem = wallet;
						wallet.OpenCommand.Execute(default);
					}
				});
		}

		public KeyManager KeyManager { get; }

		public string WalletName { get; }
	}
}
