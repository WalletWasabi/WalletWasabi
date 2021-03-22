using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class ClosedWalletViewModel : WalletViewModelBase
	{
		private readonly SmartHeaderChain _smartHeaderChain;

		[AutoNotify] private string _statusText;
		[AutoNotify] private uint _percent;

		private Stopwatch? _stopwatch;
		private uint? _startingFilterIndex;

		protected ClosedWalletViewModel(WalletManagerViewModel walletManagerViewModel, Wallet wallet)
			: base(wallet)
		{
			_smartHeaderChain = walletManagerViewModel.BitcoinStore.SmartHeaderChain;
			_statusText = " ";
			_percent = 0;

			OpenCommand = ReactiveCommand.Create(() => OnOpen(walletManagerViewModel));
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
					var segwitActivationHeight = SmartHeader.GetStartingHeader(Wallet.Network).Height;
					if (Wallet.LastProcessedFilter?.Header?.Height is { } lastProcessedFilterHeight
					    && lastProcessedFilterHeight > segwitActivationHeight
					    && _smartHeaderChain.TipHeight is { } tipHeight
					    && tipHeight > segwitActivationHeight)
					{
						var allFilters = tipHeight - segwitActivationHeight;
						var processedFilters = lastProcessedFilterHeight - segwitActivationHeight;

						UpdateStatus(allFilters, processedFilters, _stopwatch.ElapsedMilliseconds);
					}
				})
				.DisposeWith(disposables);
		}

		private void UpdateStatus(uint allFilters, uint processedFilters, double elapsedMilliseconds)
		{
			var percent = (decimal) processedFilters / allFilters * 100;
			_startingFilterIndex ??= processedFilters; // Store the filter index we started on. It is needed for better remaining time calculation.
			var realProcessedFilters = processedFilters - _startingFilterIndex.Value;
			var remainingFilterCount = allFilters - processedFilters;

			var tempPercent = (uint) Math.Round(percent);

			if (tempPercent == 0 || realProcessedFilters == 0 || remainingFilterCount == 0)
			{
				return;
			}

			Percent = tempPercent;
			var percentText = $"{Percent}% completed";

			var remainingMilliseconds = elapsedMilliseconds / realProcessedFilters * remainingFilterCount;
			var userFriendlyTime = TextHelpers.TimeSpanToFriendlyString(TimeSpan.FromMilliseconds(remainingMilliseconds));
			var remainingTimeText = string.IsNullOrEmpty(userFriendlyTime) ? "" : $"- {userFriendlyTime} remaining";

			StatusText = $"{percentText} {remainingTimeText}";
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
