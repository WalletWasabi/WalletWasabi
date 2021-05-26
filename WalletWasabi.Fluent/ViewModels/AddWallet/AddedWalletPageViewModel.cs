using System.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	[NavigationMetaData(Title = "Success")]
	public partial class AddedWalletPageViewModel : RoutableViewModel
	{
		public AddedWalletPageViewModel(KeyManager keyManager)
		{
			WalletName = keyManager.WalletName;
			WalletType = WalletHelpers.GetType(keyManager);

			SetupCancel(enableCancel: false, enableCancelOnEscape: false, enableCancelOnPressed: false);

			EnableBack = false;

			NextCommand = ReactiveCommand.Create(() => OnNext(keyManager));
		}

		public WalletType WalletType { get; }

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
