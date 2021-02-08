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
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class ClosedWalletViewModel : WalletViewModelBase
	{
		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _items;

		protected ClosedWalletViewModel(WalletManager walletManager, Wallet wallet)
			: base(wallet)
		{
			WalletManager = walletManager;
			_items = new ObservableCollection<NavBarItemViewModel>();
		}

		public WalletManager WalletManager { get; }

		public override string IconName => "web_asset_regular";

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposable)
		{
			base.OnNavigatedTo(inStack, disposable);

			if (Wallet.State == WalletState.Uninitialized)
			{
				AbandonedTasks abandonedTasks = new();
				abandonedTasks.AddAndClearCompleted(ShowLegalOrLoad());
			}
		}

		private async Task ShowLegalOrLoad()
		{
			var legalShowed = await CheckLegal();

			if (!legalShowed)
			{
				await LoadWallet();
			}
		}

		private async Task<bool> CheckLegal()
		{
			var legalChecker = (await NavigationManager.MaterialiseViewModel(LegalDocumentsViewModel.MetaData) as LegalDocumentsViewModel)?.LegalChecker;

			if (legalChecker is { } lc && lc.TryGetNewLegalDocs(out _))
			{
				var legalDocs = new TermsAndConditionsViewModel(legalChecker, this);
				Navigate().To(legalDocs);

				return true;
			}

			return false;
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
