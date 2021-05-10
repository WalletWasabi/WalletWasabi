using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.HardwareWallet;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Fluent.ViewModels.Wallets.WatchOnlyWallet;
using WalletWasabi.Gui;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets
{
	public partial class WalletViewModel : WalletViewModelBase
	{
		[AutoNotify] private IList<TileViewModel> _tiles;

		protected WalletViewModel(UiConfig uiConfig, Wallet wallet) : base(wallet)
		{
			Disposables = Disposables is null
				? new CompositeDisposable()
				: throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

			var balanceChanged =
				Observable.FromEventPattern(
						Wallet.TransactionProcessor,
						nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
					.Select(_ => Unit.Default)
					.Throttle(TimeSpan.FromSeconds(0.1))
					.Merge(Observable.FromEventPattern(Wallet, nameof(Wallet.NewFilterProcessed))
						.Select(_ => Unit.Default))
					.Merge(uiConfig.WhenAnyValue(x => x.PrivacyMode).Select(_ => Unit.Default))
					.Merge(Wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).Select(_ => Unit.Default))
					.ObserveOn(RxApp.MainThreadScheduler);

			History = new HistoryViewModel(this, uiConfig, balanceChanged);

			BalanceTile = new WalletBalanceTileViewModel(wallet, balanceChanged)
			{
				ColumnSpan = new List<int> { 1, 1, 1 },
				RowSpan = new List<int> { 1, 1, 1 }
			};
			RoundStatusTile = new RoundStatusTileViewModel(wallet)
			{
				ColumnSpan = new List<int> { 1, 1, 1 },
				RowSpan = new List<int> { 1, 1, 1 }
			};
			BtcPriceTile = new BtcPriceTileViewModel(wallet)
			{
				ColumnSpan = new List<int> { 1, 1, 1 },
				RowSpan = new List<int> { 1, 1, 1 }
			};
			WalletPieChart = new WalletPieChartTileViewModel(wallet, balanceChanged)
			{
				ColumnSpan = new List<int> { 1, 1, 1 },
				RowSpan = new List<int> { 1, 2, 2 }
			};
			BalanceChartTile = new WalletBalanceChartTileViewModel(History.UnfilteredTransactions)
			{
				ColumnSpan = new List<int> { 2, 2, 2 },
				RowSpan = new List<int> { 1, 2, 2 }
			};

			_tiles = new List<TileViewModel>
			{
				BalanceTile,
				RoundStatusTile,
				BtcPriceTile,
				WalletPieChart,
				BalanceChartTile
			};

			Observable.FromEventPattern<ProcessedResult>(Wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
				.Subscribe(arg =>
				{
					var (sender, e) = arg;

					if (uiConfig.PrivacyMode || !e.IsNews)
					{
						return;
					}

					var (title, message) = TransactionHelpers.GetNotificationInputs(e);

					NotificationHelpers.Show(title, message);
				});
		}

		private CompositeDisposable Disposables { get; set; }

		public override string IconName => "web_asset_regular";

		public HistoryViewModel History { get; }

		public WalletBalanceTileViewModel BalanceTile { get; }

		public RoundStatusTileViewModel RoundStatusTile { get; }

		public BtcPriceTileViewModel BtcPriceTile { get; }

		public WalletPieChartTileViewModel WalletPieChart { get; }

		public WalletBalanceChartTileViewModel BalanceChartTile { get; }

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);

			foreach (var tile in _tiles)
			{
				tile.Activate(disposables);
			}

			History.Activate(disposables);
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
