using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet;
using WalletWasabi.Logging;
using WalletWasabi.Nito.AsyncEx;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class ClosedWalletViewModel : WalletViewModelBase
	{
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _items;

		protected ClosedWalletViewModel(WalletManager walletManager, Wallet wallet, LegalChecker legalChecker)
			: base(wallet)
		{
			WalletManager = walletManager;
			LegalChecker = legalChecker;
			_items = new ObservableCollection<NavBarItemViewModel>();
		}

		public WalletManager WalletManager { get; }

		private LegalChecker LegalChecker { get; }

		public override string IconName => "web_asset_regular";

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			if (Wallet.State == WalletState.Uninitialized)
			{
				if (LegalChecker.TryGetNewLegalDocs(out _))
				{
					var legalDocs = new TermsAndConditionsViewModel(LegalChecker, this);
					Navigate().To(legalDocs);
				}
				else
				{
					AbandonedTasks abandonedTasks = new();
					abandonedTasks.AddAndClearCompleted(LoadWallet());
				}
			}
		}

		private async Task LoadWallet()
		{
			try
			{
				await Task.Run(async () => await WalletManager.StartWalletAsync(Wallet));
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

		public static WalletViewModelBase Create(WalletManager walletManager, Wallet wallet, LegalChecker legalChecker)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new ClosedHardwareWalletViewModel(walletManager, wallet, legalChecker)
				: wallet.KeyManager.IsWatchOnly
					? new ClosedWatchOnlyWalletViewModel(walletManager, wallet, legalChecker)
					: new ClosedWalletViewModel(walletManager, wallet, legalChecker);
		}
	}
}
