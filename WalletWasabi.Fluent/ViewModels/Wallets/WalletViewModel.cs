using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet;
using WalletWasabi.Gui;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class WalletViewModel : WalletViewModelBase
	{
		protected WalletViewModel(UiConfig uiConfig, Wallet wallet) : base(wallet)
		{
			Disposables = Disposables is null ? new CompositeDisposable() : throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			Observable.Merge(
				Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.Merge(uiConfig.WhenAnyValue(x => x.PrivacyMode).Select(_ => Unit.Default))
				.Merge(Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(
					_ =>
				{
					try
					{
						var balance = Wallet.Coins.TotalAmount();
						Title = $"{WalletName} ({(uiConfig.PrivacyMode ? "#########" : balance.ToString(false))} BTC)";

						TitleTip = balance.ToUsdString(Wallet.Synchronizer.UsdExchangeRate, uiConfig.PrivacyMode);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex);
					}
				})
				.DisposeWith(Disposables);

			History = new HistoryViewModel(wallet, uiConfig);
			BalanceTile = new WalletBalanceTileViewModel(wallet);
			BalanceChartTile = new WalletBalanceChartTileViewModel(History.Transactions);
			WalletPieChart = new WalletPieChartTileViewModel(wallet);
		}

		private CompositeDisposable Disposables { get; set; }

		public override string IconName => "web_asset_regular";

		public HistoryViewModel History { get; }

		public WalletBalanceTileViewModel BalanceTile { get; }

		public WalletBalanceChartTileViewModel BalanceChartTile { get; }

		public WalletPieChartTileViewModel WalletPieChart { get; }

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			Observable.FromEventPattern(Wallet, nameof(Wallet.NewFilterProcessed))
				.Merge(Observable.FromEventPattern(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed)))
				.Throttle(TimeSpan.FromSeconds(3))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async _ => await UpdateAsync())
				.DisposeWith(disposables);

			RxApp.MainThreadScheduler.Schedule(async () => await UpdateAsync());
		}

		private async Task UpdateAsync()
		{
			await History.UpdateAsync();
		}

		public static WalletViewModel Create(UiConfig uiConfig, Wallet wallet)
		{
			return wallet.KeyManager.IsHardwareWallet
				? new HardwareWalletViewModel(uiConfig, wallet)
				: wallet.KeyManager.IsWatchOnly
					? new WatchOnlyWalletViewModel(uiConfig, wallet)
					: new WalletViewModel(uiConfig, wallet);
		}
	}
}
