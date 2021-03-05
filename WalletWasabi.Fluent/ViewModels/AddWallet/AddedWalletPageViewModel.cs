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
			WalletIcon = keyManager.Icon;
			IsHardwareWallet = keyManager.IsHardwareWallet;
			WalletName = keyManager.WalletName;

			NextCommand = ReactiveCommand.Create(() => NextExecute(walletManager, keyManager));
		}
		public string? WalletIcon { get; }

		public bool IsHardwareWallet { get; }

		public string WalletName { get; }

		private void NextExecute(WalletManager walletManager, KeyManager keyManager)
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
		}
	}
}
