using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using NBitcoin;
using ReactiveUI;
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
	[AutoNotify] private DebugTransactionViewModel? _selectedTransaction;

	public DebugWalletViewModel(Wallet wallet)
	{
		_wallet = wallet;

		if (wallet.Coins is { })
		{
			_coins = ((CoinsRegistry) wallet.Coins).AsAllCoinsView();
		}

		WalletName = _wallet.WalletName;

		Coins = _coins?.Select(x => new DebugCoinViewModel(x)).ToList();

		// TODO: Transactions
		Transactions = new List<DebugTransactionViewModel>();

		if (_coins is { })
		{
			foreach (var coin in _coins)
			{
				Transactions.Add(new DebugTransactionViewModel(coin.Transaction));

				if (coin.SpenderTransaction is { })
				{
					Transactions.Add(new DebugTransactionViewModel(coin.SpenderTransaction));
				}
			}
		}

		CreateCoinsSource();

		CreateTransactionsSource();
	}

	public string WalletName { get; private set; }

	public List<DebugCoinViewModel>? Coins { get; }

	public List<DebugTransactionViewModel>? Transactions { get; }

	public FlatTreeDataGridSource<DebugCoinViewModel> CoinsSource { get; private set; }

	public FlatTreeDataGridSource<DebugTransactionViewModel> TransactionsSource { get; private set; }

	private void CreateCoinsSource()
	{
		var coins = Coins ?? Enumerable.Empty<DebugCoinViewModel>();

		CoinsSource = new FlatTreeDataGridSource<DebugCoinViewModel>(coins)
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

	private void CreateTransactionsSource()
	{
		var transactions = Transactions ?? Enumerable.Empty<DebugTransactionViewModel>();

		TransactionsSource = new FlatTreeDataGridSource<DebugTransactionViewModel>(transactions)
		{
			Columns =
			{
				new TextColumn<DebugTransactionViewModel, DateTimeOffset>(
					"FirstSeen",
					x => x.FirstSeen,
					new GridLength(0, GridUnitType.Auto)),
			}
		};

		TransactionsSource.RowSelection!.SingleSelect = true;

		TransactionsSource.RowSelection
			.WhenAnyValue(x => x.SelectedItem)
			.Subscribe(x => SelectedTransaction = x);
	}
}
