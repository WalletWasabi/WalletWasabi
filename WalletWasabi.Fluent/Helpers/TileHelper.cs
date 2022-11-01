using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

namespace WalletWasabi.Fluent.Helpers;

public static class TileHelper
{
	public static List<TileViewModel> GetWalletTiles(WalletViewModel walletVm)
	{
		var isWatchOnly = walletVm.Wallet.KeyManager.IsWatchOnly;
		var hasBalance = walletVm.IsWalletBalanceZero;

		var priceTileColumn = 1;

		var tiles = new List<TileViewModel>
		{
			new WalletBalanceTileViewModel(walletVm)
			{
				TilePresets = new ObservableCollection<TilePresetViewModel>()
				{
					new(column: 0, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
					new(column: 0, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
					new(column: 0, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium)
				},
				TilePresetIndex = walletVm.LayoutIndex
			}
		};

		if (!isWatchOnly || hasBalance)
		{
			tiles.Add(new PrivacyControlTileViewModel(walletVm)
			{
				TilePresets = new ObservableCollection<TilePresetViewModel>()
				{
					new(column: 1, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
					new(column: 1, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
					new(column: 1, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
				},
				TilePresetIndex = walletVm.LayoutIndex
			});

			priceTileColumn = 2;
		}

		tiles.Add(new BtcPriceTileViewModel(walletVm.Wallet)
		{
			TilePresets = new ObservableCollection<TilePresetViewModel>()
			{
				new(column: priceTileColumn, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
				new(column: priceTileColumn, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
				new(column: priceTileColumn, row: 0, columnSpan: 1, rowSpan: 1, TileSize.Medium),
			},
			TilePresetIndex = walletVm.LayoutIndex
		});

		return tiles;
	}

	public static IList<TileLayoutViewModel> GetWalletLayout(WalletViewModel walletVm)
	{
		var isWatchOnly = walletVm.Wallet.KeyManager.IsWatchOnly;
		var hasBalance = walletVm.IsWalletBalanceZero;

		var columns = isWatchOnly || hasBalance ? "316,316" : "316,316,316";
		return new ObservableCollection<TileLayoutViewModel>()
		{
			new("Small", columnDefinitions: columns, rowDefinitions: "150"),
			new("Normal", columnDefinitions: columns, rowDefinitions: "150"),
			new("Wide", columnDefinitions: columns, rowDefinitions: "150")
		};
	}
}
