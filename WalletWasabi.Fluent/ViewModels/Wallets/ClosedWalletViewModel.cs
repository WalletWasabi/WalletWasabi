using System.Collections.ObjectModel;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class ClosedWalletViewModel : WalletViewModelBase
	{
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _items;
		[AutoNotify] private string _estimationText;
		[AutoNotify] private double _percent;

		protected ClosedWalletViewModel(WalletManagerViewModel walletManagerViewModel, Wallet wallet)
			: base(wallet)
		{
			_items = new ObservableCollection<NavBarItemViewModel>();

			_estimationText = "55% completed - 25 minutes remaining";
			_percent = 55;

			OpenCommand = ReactiveCommand.Create(() =>
			{
				if (!Wallet.IsLoggedIn)
				{
					Navigate().To(new LoginViewModel(walletManagerViewModel, this), NavigationMode.Clear);
				}
				else
				{
					Navigate().To(this, NavigationMode.Clear);
				}
			});
		}

		public override string IconName => "web_asset_regular";

		public static WalletViewModelBase Create(WalletManagerViewModel walletManager, Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new ClosedHardwareWalletViewModel(walletManager, wallet)
				: wallet.KeyManager.IsWatchOnly
					? new ClosedWatchOnlyWalletViewModel(walletManager, wallet)
					: new ClosedWalletViewModel(walletManager, wallet);
		}
	}
}
