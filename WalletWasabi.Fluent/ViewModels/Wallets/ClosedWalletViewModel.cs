using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Blocks;
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
		[AutoNotify] private decimal _percent;

		private SmartHeaderChain _smartHeaderChain;

		protected ClosedWalletViewModel(WalletManagerViewModel walletManagerViewModel, Wallet wallet)
			: base(wallet)
		{
			_items = new ObservableCollection<NavBarItemViewModel>();
			_smartHeaderChain = walletManagerViewModel.BitcoinStore.SmartHeaderChain;
			_estimationText = "";
			_percent = 0;

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

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			IDisposable? walletCheckingInterval = null;
			Observable.FromEventPattern<bool>(typeof(Wallet), nameof(Wallet.InitializingChanged))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					if (x.EventArgs && x.Sender is Wallet wallet)
					{
						walletCheckingInterval ??= Observable.Interval(TimeSpan.FromSeconds(1))
							.ObserveOn(RxApp.MainThreadScheduler)
							.Subscribe(_ =>
							{
								var segwitActivationHeight = SmartHeader.GetStartingHeader(wallet.Network).Height;
								if (wallet.LastProcessedFilter?.Header?.Height is uint lastProcessedFilterHeight
								    && lastProcessedFilterHeight > segwitActivationHeight
								    && _smartHeaderChain.TipHeight is uint tipHeight
								    && tipHeight > segwitActivationHeight)
								{
									var allFilters = tipHeight - segwitActivationHeight;
									var processedFilters = lastProcessedFilterHeight - segwitActivationHeight;
									var perc = allFilters == 0
										? 100
										: ((decimal) processedFilters / allFilters * 100);

									Percent = perc;
									EstimationText = $"{Percent}% completed - 25 minutes remaining";
								}
							})
							.DisposeWith(disposables);
					}
					else
					{
						walletCheckingInterval?.Dispose();
						walletCheckingInterval = null;
					}
				})
				.DisposeWith(disposables);
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
