using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class ClosedWalletViewModel : WalletViewModelBase
	{
		private readonly WalletManager _walletManager;
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _items;

		protected ClosedWalletViewModel(WalletManager walletManager, Wallet wallet)
			: base(wallet)
		{
			_walletManager = walletManager;
			_items = new ObservableCollection<NavBarItemViewModel>();

			OpenCommand = ReactiveCommand.Create(() =>
			{
				if (!Wallet.IsLoggedIn)
				{
					Navigate().To(new LoginViewModel(this));
				}
			});
		}

		public override string IconName => "web_asset_regular";

		public async Task LoadWallet()
		{
			if (Wallet.State != WalletState.Uninitialized)
			{
				return;
			}

			try
			{
				await Task.Run(async () => await _walletManager.StartWalletAsync(Wallet));
			}
			catch (OperationCanceledException ex)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				await ShowErrorAsync(ex.ToUserFriendlyString(), "Wasabi was unable to load your wallet");
				Logger.LogError(ex);
			}
		}

		public static WalletViewModelBase Create(WalletManager walletManager, Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new ClosedHardwareWalletViewModel(walletManager, wallet)
				: wallet.KeyManager.IsWatchOnly
					? new ClosedWatchOnlyWalletViewModel(walletManager, wallet)
					: new ClosedWalletViewModel(walletManager, wallet);
		}
	}
}
