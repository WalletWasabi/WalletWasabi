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
						new(0, 0, 1, 1, TileSize.Medium),
						new(0, 0, 1, 1, TileSize.Medium),
						new(0, 0, 1, 1, TileSize.Medium)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new BtcPriceTileViewModel(walletViewModel.Wallet)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(2, 0, 1, 1, TileSize.Medium),
						new(0, 1, 1, 1, TileSize.Medium),
						new(1, 0, 1, 1, TileSize.Medium)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new WalletPieChartTileViewModel(walletViewModel, balanceChanged)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(1, 0, 1, 1, TileSize.Medium),
						new(1, 0, 1, 2, TileSize.Large),
						new(0, 1, 2, 1, TileSize.Wide)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new WalletBalanceChartTileViewModel(walletViewModel.History.UnfilteredTransactions)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(3, 0, 1, 1, TileSize.Medium),
						new(2, 0, 1, 2, TileSize.Wide),
						new(0, 2, 2, 1, TileSize.Wide)
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
						new(0, 0, 1, 1, TileSize.Medium),
						new(0, 0, 1, 1, TileSize.Medium),
						new(0, 0, 1, 1, TileSize.Medium)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new BtcPriceTileViewModel(walletViewModel.Wallet)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(1, 0, 1, 1, TileSize.Medium),
						new(0, 1, 1, 1, TileSize.Medium),
						new(1, 0, 1, 1, TileSize.Medium)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},

				new WalletBalanceChartTileViewModel(walletViewModel.History.UnfilteredTransactions)
				{
					TilePresets = new ObservableCollection<TilePresetViewModel>()
					{
						new(2, 0, 1, 1, TileSize.Medium),
						new(1, 0, 2, 2, TileSize.Wide),
						new(0, 1, 2, 1, TileSize.Wide)
					},
					TilePresetIndex = walletViewModel.LayoutIndex
				},
			};
		}
	}
}
