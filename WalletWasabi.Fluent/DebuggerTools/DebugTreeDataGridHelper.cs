using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.DebuggerTools.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.DebuggerTools;

public static class DebugTreeDataGridHelper
{
	public static FlatTreeDataGridSource<DebugCoinViewModel> CreateCoinsSource(
		IEnumerable<DebugCoinViewModel> coins,
		Action<DebugCoinViewModel?> select)
	{
		var coinsSource = new FlatTreeDataGridSource<DebugCoinViewModel>(coins)
		{
			Columns =
			{
				new TextColumn<DebugCoinViewModel, DateTimeOffset>(
					"FirstSeen",
					x => x.FirstSeen,
					new GridLength(0, GridUnitType.Auto)),
				new TextColumn<DebugCoinViewModel, Money>(
					"Amount",
					x => x.Amount,
					new GridLength(0, GridUnitType.Auto)),
				new TextColumn<DebugCoinViewModel, bool>(
					"Confirmed",
					x => x.Confirmed,
					new GridLength(0, GridUnitType.Auto)),
				new TextColumn<DebugCoinViewModel, bool>(
					"CoinJoinInProgress",
					x => x.CoinJoinInProgress,
					new GridLength(0, GridUnitType.Auto)),
				new TextColumn<DebugCoinViewModel, bool>(
					"IsBanned",
					x => x.IsBanned,
					new GridLength(0, GridUnitType.Auto)),
				new TextColumn<DebugCoinViewModel, DateTimeOffset?>(
					"BannedUntilUtc",
					x => x.BannedUntilUtc,
					new GridLength(0, GridUnitType.Auto)),
				new TextColumn<DebugCoinViewModel, Height?>(
					"Height",
					x => x.Height,
					new GridLength(0, GridUnitType.Auto)),
				new TextColumn<DebugCoinViewModel, uint256>(
					"Transaction",
					x => x.TransactionId,
					new GridLength(0, GridUnitType.Auto)),
				new TextColumn<DebugCoinViewModel, uint256?>(
					"SpenderTransaction",
					x => x.SpenderTransactionId,
					new GridLength(0, GridUnitType.Auto)),
			}
		};

		coinsSource.RowSelection!.SingleSelect = true;

		coinsSource.RowSelection
			.WhenAnyValue(x => x.SelectedItem)
			.Subscribe(select);

		(coinsSource as ITreeDataGridSource).SortBy(coinsSource.Columns[0], ListSortDirection.Descending);

		return coinsSource;
	}

	public static FlatTreeDataGridSource<DebugTransactionViewModel> CreateTransactionsSource(
		IEnumerable<DebugTransactionViewModel> transactions,
		Action<DebugTransactionViewModel?> select)
	{
		var transactionsSource = new FlatTreeDataGridSource<DebugTransactionViewModel>(transactions)
		{
			Columns =
			{
				new TextColumn<DebugTransactionViewModel, DateTimeOffset>(
					"FirstSeen",
					x => x.FirstSeen,
					new GridLength(0, GridUnitType.Auto)),
				new TextColumn<DebugTransactionViewModel, uint256>(
					"TransactionId",
					x => x.TransactionId,
					new GridLength(0, GridUnitType.Auto)),
			}
		};

		transactionsSource.RowSelection!.SingleSelect = true;

		transactionsSource.RowSelection
			.WhenAnyValue(x => x.SelectedItem)
			.Subscribe(select);

		(transactionsSource as ITreeDataGridSource).SortBy(transactionsSource.Columns[0], ListSortDirection.Descending);

		return transactionsSource;
	}
}
