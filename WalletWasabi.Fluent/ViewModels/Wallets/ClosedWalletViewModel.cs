using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Fluent.Helpers;
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
		private readonly Wallet _wallet;
		private readonly SmartHeaderChain _smartHeaderChain;

		[AutoNotify] private ObservableCollection<NavBarItemViewModel> _items;
		[AutoNotify] private string _estimationText;
		[AutoNotify] private uint _percent;

		private Stopwatch? _stopwatch;
		private uint? _startingPercent;

		protected ClosedWalletViewModel(WalletManagerViewModel walletManagerViewModel, Wallet wallet)
			: base(wallet)
		{
			_wallet = wallet;
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

			_stopwatch ??= Stopwatch.StartNew();

			Observable.Interval(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					var segwitActivationHeight = SmartHeader.GetStartingHeader(_wallet.Network).Height;
					if (_wallet.LastProcessedFilter?.Header?.Height is { } lastProcessedFilterHeight
					    && lastProcessedFilterHeight > segwitActivationHeight
					    && _smartHeaderChain.TipHeight is { } tipHeight
					    && tipHeight > segwitActivationHeight)
					{
						var allFilters = tipHeight - segwitActivationHeight;
						var processedFilters = lastProcessedFilterHeight - segwitActivationHeight;
						var perc = allFilters == 0
							? 100
							: (decimal) processedFilters / allFilters * 100;

						Percent = (uint)Math.Round(perc);

						// Store the percentage we started on. It is needed for better remaining time calculation.
						_startingPercent ??= Percent;

						SetStatusText(Percent, _startingPercent.Value, _stopwatch);
					}
				})
				.DisposeWith(disposables);
		}

		private void SetStatusText(uint percent, uint startingPercent, Stopwatch stopwatch)
		{
			if (percent == 0)
			{
				return;
			}

			var percentText = $"{percent}% completed";

			var remainingMilliseconds = (stopwatch.Elapsed.TotalMilliseconds / percent - startingPercent) * (100 - percent);
			var userFriendlyTime = TextHelpers.TimeSpanToFriendlyString(TimeSpan.FromMilliseconds(remainingMilliseconds));
			var remainingTimeText = string.IsNullOrEmpty(userFriendlyTime) ? "" : $"- {userFriendlyTime} remaining";

			EstimationText = $"{percentText} {remainingTimeText}";
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
