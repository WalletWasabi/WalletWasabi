using System.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Helpers;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(Title = "Success")]
	public partial class AddedWalletPageViewModel : RoutableViewModel
	{
		public AddedWalletPageViewModel(KeyManager keyManager)
		{
			Guard.NotNull(nameof(Services.WalletManager), Services.WalletManager);

			WalletIcon = keyManager.Icon;
			IsHardwareWallet = keyManager.IsHardwareWallet;
			WalletName = keyManager.WalletName;

			SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

			EnableBack = false;

			NextCommand = ReactiveCommand.Create(() => OnNext(keyManager));
		}

		public string? WalletIcon { get; }

		public bool IsHardwareWallet { get; }

		public string WalletName { get; }

		private void OnNext(KeyManager keyManager)
		{
			Services.WalletManager.AddWallet(keyManager);

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
