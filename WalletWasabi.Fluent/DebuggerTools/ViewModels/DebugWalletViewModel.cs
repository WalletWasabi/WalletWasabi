using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugWalletViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposable;
	private readonly Wallet _wallet;
	private readonly IObservable<Unit> _updateTrigger;
	private ICoinsView? _coinsView;
	[AutoNotify] private DebugCoinViewModel? _selectedCoin;
	[AutoNotify] private DebugTransactionViewModel? _selectedTransaction;
	[AutoNotify] private DebugLogItemViewModel? _selectedLogItem;
	[AutoNotify] private bool _autoScrollLogItems = true;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<DebugLogItemViewModel> _logItems;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<DebugCoinViewModel> _coins;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<DebugTransactionViewModel> _transactions;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private FlatTreeDataGridSource<DebugCoinViewModel>? _coinsSource;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private FlatTreeDataGridSource<DebugTransactionViewModel>? _transactionsSource;

	public DebugWalletViewModel(Wallet wallet)
	{
		_disposable = new CompositeDisposable();
		_wallet = wallet;

		WalletName = _wallet.WalletName;

		LogItems = new ObservableCollection<DebugLogItemViewModel>();

		Coins = new ObservableCollection<DebugCoinViewModel>();

		Transactions = new ObservableCollection<DebugTransactionViewModel>();

		_updateTrigger =
			Observable
				.FromEventPattern(_wallet, nameof(Wallet.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default)
				.Merge(Observable.FromEventPattern(_wallet, nameof(Wallet.NewFilterProcessed)).Select(_ => Unit.Default))
				.Throttle(TimeSpan.FromSeconds(0.1))
				.ObserveOn(RxApp.MainThreadScheduler);

		_updateTrigger
			.SubscribeAsync(async _ => await Task.Run(Update))
			.DisposeWith(_disposable);

		Observable
			.FromEventPattern<ProcessedResult>(_wallet, nameof(Wallet.WalletRelevantTransactionProcessed))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(processedResult =>
			{
				var logItem = new DebugTransactionProcessedLogItemViewModel(processedResult);

				LogItems.Add(logItem);

				if (AutoScrollLogItems)
				{
					SelectedLogItem = logItem;
				}
			})
			.DisposeWith(_disposable);

		Observable
			.FromEventPattern<FilterModel>(_wallet, nameof(Wallet.NewFilterProcessed))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(filterModel =>
			{
				var logItem = new DebugNewFilterProcessedLogItemViewModel(filterModel);
				LogItems.Add(logItem);

				if (AutoScrollLogItems)
				{
					SelectedLogItem = logItem;
				}
			})
			.DisposeWith(_disposable);

		Observable
			.FromEventPattern<Block>(_wallet, nameof(Wallet.NewBlockProcessed))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(block =>
			{
				var logItem = new DebugNewBlockProcessedLogItemViewModel(block);

				LogItems.Add(logItem);

				if (AutoScrollLogItems)
				{
					SelectedLogItem = logItem;
				}
			})
			.DisposeWith(_disposable);

		Observable
			.FromEventPattern<WalletState>(_wallet, nameof(Wallet.StateChanged))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.SubscribeAsync(async state =>
			{
				var logItem = new DebugStateChangedLogItemViewModel(state);

				LogItems.Add(logItem);

				if (AutoScrollLogItems)
				{
					SelectedLogItem = logItem;
				}

				if (state == WalletState.Started)
				{
					await Task.Run(Update);
				}
			})
			.DisposeWith(_disposable);

		Update();

		Dispatcher.UIThread.InvokeAsync(() =>
		{
			CoinsSource?.Dispose();
			CoinsSource = DebugTreeDataGridHelper.CreateCoinsSource(
				Coins,
				x => SelectedCoin = x);

			TransactionsSource?.Dispose();
			TransactionsSource = DebugTreeDataGridHelper.CreateTransactionsSource(
				Transactions,
				x => SelectedTransaction = x);
		});
	}

	public string WalletName { get; private set; }

	private void Update()
	{
		if (_wallet.Coins is { })
		{
			_coinsView = ((CoinsRegistry)_wallet.Coins).AsAllCoinsView();
		}

		var selectedCoinId = SelectedCoin?.TransactionId;
		var selectedTransactionId = SelectedTransaction?.TransactionId;

		foreach (var coin in Coins)
		{
			coin.Dispose();
		}

		foreach (var transaction in Transactions)
		{
			transaction.Dispose();
		}

		Coins.Clear();
		SelectedCoin = null;

		Transactions.Clear();
		SelectedTransaction = null;

		var newCoins = new List<DebugCoinViewModel>();
		var newTransactions = new List<DebugTransactionViewModel>();

		if (_coinsView is { })
		{
			var coins = _coinsView.Select(x => new DebugCoinViewModel(x, _updateTrigger));

			foreach (var coin in coins)
			{
				newCoins.Add(coin);
			}

			var transactionsDict = MapTransactions(newCoins);

			var existingTransactions = new HashSet<uint256>();

			foreach (var coin in newCoins)
			{
				if (!existingTransactions.Contains(coin.TransactionId))
				{
					coin.Transaction.Coins.AddRange(transactionsDict[coin.TransactionId]);

					newTransactions.Add(coin.Transaction);

					existingTransactions.Add(coin.TransactionId);
				}

				if (coin.SpenderTransactionId is { } && coin.SpenderTransaction is { })
				{
					if (!existingTransactions.Contains(coin.SpenderTransactionId))
					{
						coin.SpenderTransaction.Coins.AddRange(transactionsDict[coin.SpenderTransactionId]);

						newTransactions.Add(coin.SpenderTransaction);

						existingTransactions.Add(coin.SpenderTransactionId);
					}
				}
			}
		}

		Coins.AddRange(newCoins);
		Transactions.AddRange(newTransactions);

		if (selectedCoinId is { })
		{
			var coin = Coins.FirstOrDefault(x => x.TransactionId == selectedCoinId);
			if (coin is { })
			{
				SelectedCoin = coin;
			}
		}

		if (selectedTransactionId is { })
		{
			var transaction = Transactions.FirstOrDefault(x => x.TransactionId == selectedTransactionId);
			if (transaction is { })
			{
				SelectedTransaction = transaction;
			}
		}
	}

	private Dictionary<uint256, List<DebugCoinViewModel>> MapTransactions(List<DebugCoinViewModel> coins)
	{
		var transactionsDict = new Dictionary<uint256, List<DebugCoinViewModel>>();

		foreach (var coin in coins)
		{
			if (transactionsDict.TryGetValue(coin.TransactionId, out _))
			{
				transactionsDict[coin.TransactionId].Add(coin);
			}
			else
			{
				transactionsDict[coin.TransactionId] = new List<DebugCoinViewModel> { coin };
			}

			if (coin.SpenderTransactionId is null)
			{
				continue;
			}

			if (transactionsDict.TryGetValue(coin.SpenderTransactionId, out _))
			{
				transactionsDict[coin.SpenderTransactionId].Add(coin);
			}
			else
			{
				transactionsDict[coin.SpenderTransactionId] = new List<DebugCoinViewModel> { coin };
			}
		}

		return transactionsDict;
	}

	public void Dispose()
	{
		_disposable.Dispose();

		_coinsSource?.Dispose();
		_transactionsSource?.Dispose();

		foreach (var coin in _coins)
		{
			coin.Dispose();
		}

		foreach (var transaction in _transactions)
		{
			transaction.Dispose();
		}
	}
}
