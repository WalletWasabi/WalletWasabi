using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugTransactionViewModel : ViewModelBase
{
	private readonly SmartTransaction _transaction;
	private readonly IObservable<Unit> _updateTrigger;
	[AutoNotify] private DebugCoinViewModel? _selectedCoin;

	public DebugTransactionViewModel(SmartTransaction transaction, IObservable<Unit> updateTrigger)
	{
		_transaction = transaction;
		_updateTrigger = updateTrigger;

		Update();

		CreateCoinsSource();
	}

	private void Update()
	{
		FirstSeen = _transaction.FirstSeen.LocalDateTime;

		TransactionId = _transaction.GetHash();

		Coins = new ObservableCollection<DebugCoinViewModel>();
	}

	public SmartTransaction Transaction => _transaction;

	public DateTimeOffset FirstSeen { get; private set; }

	public uint256 TransactionId { get; private set; }

	public ObservableCollection<DebugCoinViewModel> Coins { get; private set; }

	public FlatTreeDataGridSource<DebugCoinViewModel> CoinsSource { get; private set; }

	private void CreateCoinsSource()
	{
		CoinsSource = new FlatTreeDataGridSource<DebugCoinViewModel>(Coins)
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

		CoinsSource.RowSelection!.SingleSelect = true;

		CoinsSource.RowSelection
			.WhenAnyValue(x => x.SelectedItem)
			.Subscribe(x => SelectedCoin = x);

		(CoinsSource as ITreeDataGridSource).SortBy(CoinsSource.Columns[0], ListSortDirection.Descending);
	}
}
