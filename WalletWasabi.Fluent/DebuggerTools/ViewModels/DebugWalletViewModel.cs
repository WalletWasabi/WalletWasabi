using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugWalletViewModel : ViewModelBase
{
	private readonly Wallet _wallet;
	private readonly ICoinsView? _coins;
	[AutoNotify] private DebugCoinViewModel? _selectedCoin;

	public DebugWalletViewModel(Wallet wallet)
	{
		_wallet = wallet;

		if (wallet.Coins is { })
		{
			_coins = ((CoinsRegistry) wallet.Coins).AsAllCoinsView();
		}

		WalletName = _wallet.WalletName;
		Coins = _coins?.Select(x => new DebugCoinViewModel(x)).ToList();

		CreateCoinsSource();
	}

	private void CreateCoinsSource()
	{
		CoinsSource = new FlatTreeDataGridSource<DebugCoinViewModel>(Coins ?? Enumerable.Empty<DebugCoinViewModel>())
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
			}
		};

		CoinsSource.RowSelection!.SingleSelect = true;

		CoinsSource.RowSelection
			.WhenAnyValue(x => x.SelectedItem)
			.Subscribe(x => SelectedCoin = x);
	}

	public string WalletName { get; private set; }

	public List<DebugCoinViewModel>? Coins { get; }

	public FlatTreeDataGridSource<DebugCoinViewModel> CoinsSource { get; private set; }
}
