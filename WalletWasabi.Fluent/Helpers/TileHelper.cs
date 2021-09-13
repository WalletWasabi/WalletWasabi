using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

namespace WalletWasabi.Fluent.Helpers
{
	public static class TileHelper
	{
		public static List<TileViewModel> GetNormalWalletTiles(WalletViewModel walletViewModel, IObservable<Unit> balanceChanged)
		{
			return new List<TileViewModel>
			{
				new WalletBalanceTileViewModel(walletViewModel.Wallet, balanceChanged, walletViewModel.History.UnfilteredTransactions)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(column: 0, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 0, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 0, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new BtcPriceTileViewModel(walletViewModel.Wallet)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(column: 2, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 0, row: 1, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 1, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new WalletPieChartTileViewModel(walletViewModel, balanceChanged)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(column: 1, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 1, row: 0, columnSpan: 1, rowSpan: 2, TileSize.Large),
						new(column: 0, row: 1, columnSpan: 2, rowSpan: 1, TileSize.Wide)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new WalletBalanceChartTileViewModel(walletViewModel.History.UnfilteredTransactions)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(column: 3, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 2, row: 0, columnSpan: 1, rowSpan: 2, TileSize.Wide),
						new(column: 0, row: 2, columnSpan: 2, rowSpan: 1, TileSize.Wide)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},
			};
		}

		public static List<TileViewModel> GetWatchOnlyWalletTiles(WalletViewModel walletViewModel, IObservable<Unit> balanceChanged)
		{
			return new List<TileViewModel>
			{
				new WalletBalanceTileViewModel(walletViewModel.Wallet, balanceChanged, walletViewModel.History.UnfilteredTransactions)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(column: 0, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 0, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 0, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new BtcPriceTileViewModel(walletViewModel.Wallet)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(column: 1, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 0, row: 1, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 1, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new WalletBalanceChartTileViewModel(walletViewModel.History.UnfilteredTransactions)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(column: 2, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
						new(column: 1, row: 0, columnSpan: 1, rowSpan: 2, TileSize.Wide),
						new(column: 0, row: 1, columnSpan: 2, rowSpan: 1, TileSize.Wide)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},
			};
		}

		public static IList<TileLayoutViewModel> GetWatchOnlyWalletLayout()
		{
			return new ObservableCollection<TileLayoutViewModel>()
			{
				new("Small", columnDefinitions:"330,330,330", rowDefinitions:"150"),
				new("Normal", columnDefinitions:"330,660", rowDefinitions:"150,150"),
				new("Wide", columnDefinitions: "330,330", rowDefinitions: "150,300")
			};
		}

		public static IList<TileLayoutViewModel> GetNormalWalletLayout()
		{
			return new ObservableCollection<TileLayoutViewModel>()
			{
				new("Small", columnDefinitions: "330,330,330,330", rowDefinitions: "150"),
				new("Normal", columnDefinitions: "330,330,330", rowDefinitions: "150,150"),
				new("Wide", columnDefinitions: "330,330", rowDefinitions: "150,300,300")
			};
		}
	}
}
