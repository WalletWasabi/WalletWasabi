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
	public class AddedWalletPageViewModel : RoutableViewModel
	{
		public AddedWalletPageViewModel(WalletManager walletManager, KeyManager keyManager)
		{
			Title = "Success";
			WalletName = keyManager.WalletName;

			Type = Enum.TryParse(typeof(WalletType), keyManager.Icon, true, out var typ) && typ is { }
				? (WalletType)typ
				: keyManager.IsHardwareWallet
					? WalletType.Hardware
					: WalletType.Normal;

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
						Navigate(NavigationTarget.HomeScreen).To(wallet, NavigationMode.Clear);
					}
				});
		}

		public WalletType Type { get; }

		public string WalletName { get; }
	}
}
