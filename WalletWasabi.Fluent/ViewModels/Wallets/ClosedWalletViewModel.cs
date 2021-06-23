using System.Reactive.Disposables;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public class ClosedWalletViewModel : WalletViewModelBase
	{
		protected ClosedWalletViewModel(WalletManagerViewModel walletManagerViewModel, Wallet wallet)
			: base(wallet)
		{
			Loading = new LoadingViewModel(wallet);
			OpenCommand = ReactiveCommand.Create(() => OnOpen(walletManagerViewModel));
		}

		public LoadingViewModel Loading { get; }

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			Loading.Activate(disposables);
		}

		private void OnOpen(WalletManagerViewModel walletManagerViewModel)
		{
			if (!Wallet.IsLoggedIn)
			{
				Navigate().To(new LoginViewModel(walletManagerViewModel, this), NavigationMode.Clear);
			}
			else
			{
				Navigate().To(this, NavigationMode.Clear);
			}
		}

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
