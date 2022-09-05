using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
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

public partial class DebugWalletViewModel : ViewModelBase
{
	private readonly Wallet _wallet;
	private readonly IObservable<Unit> _updateTrigger;
	private ICoinsView? _coinsView;
	[AutoNotify] private DebugCoinViewModel? _selectedCoin;
	[AutoNotify] private DebugTransactionViewModel? _selectedTransaction;
	[AutoNotify] private DebugLogItemViewModel? _selectedLogItem;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<DebugLogItemViewModel> _logItems;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<DebugCoinViewModel> _coins;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<DebugTransactionViewModel> _transactions;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private FlatTreeDataGridSource<DebugCoinViewModel> _coinsSource;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private FlatTreeDataGridSource<DebugTransactionViewModel> _transactionsSource;

	public DebugWalletViewModel(Wallet wallet)
	{
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

		_updateTrigger.SubscribeAsync(async _ => await Task.Run(Update));

		Observable
			.FromEventPattern<ProcessedResult>(_wallet, nameof(Wallet.WalletRelevantTransactionProcessed))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(processedResult =>
			{
				var logItem = new DebugTransactionProcessedLogItemViewModel(processedResult);
				LogItems.Add(logItem);
				SelectedLogItem = logItem;
			});

		Observable
			.FromEventPattern<FilterModel>(_wallet, nameof(Wallet.NewFilterProcessed))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(filterModel =>
			{
				var logItem = new DebugNewFilterProcessedLogItemViewModel(filterModel);
				LogItems.Add(logItem);
				SelectedLogItem = logItem;
			});

		Observable
			.FromEventPattern<Block>(_wallet, nameof(Wallet.NewBlockProcessed))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(block =>
			{
				var logItem = new DebugNewBlockProcessedLogItemViewModel(block);
				LogItems.Add(logItem);
				SelectedLogItem = logItem;
			});

		Observable
			.FromEventPattern<WalletState>(_wallet, nameof(Wallet.StateChanged))
			.Select(x => x.EventArgs)
			.ObserveOn(RxApp.MainThreadScheduler)
			.SubscribeAsync(async state =>
			{
				var logItem = new DebugStateChangedLogItemViewModel(state);
				LogItems.Add(logItem);
				SelectedLogItem = logItem;

				if (state == WalletState.Started)
				{
					await Task.Run(Update);
				}
			});

		Update();

		Dispatcher.UIThread.InvokeAsync(() =>
		{
			CoinsSource = DebugTreeDataGridHelper.CreateCoinsSource(
				Coins,
				x => SelectedCoin = x);

			TransactionsSource = DebugTreeDataGridHelper.CreateTransactionsSource(
				Transactions,
				x => SelectedTransaction = x);
		});
	}

	private void Update()
	{
		if (_wallet.Coins is { })
		{
			_coinsView = ((CoinsRegistry)_wallet.Coins).AsAllCoinsView();
		}

		var selectedCoin = SelectedCoin;
		var selectedTransaction = SelectedTransaction;

		Coins.Clear();
		SelectedCoin = null;

		Transactions.Clear();
		SelectedTransaction = null;

		if (_coinsView is { })
		{
			var coins = _coinsView.Select(x => new DebugCoinViewModel(x, _updateTrigger));

			foreach (var coin in coins)
			{
				Coins.Add(coin);
			}

			var transactionsDict = MapTransactions();

			var existingTransactions = new HashSet<uint256>();

			foreach (var coin in Coins)
			{
				if (!existingTransactions.Contains(coin.TransactionId))
				{
					foreach (var transactionCoin in transactionsDict[coin.TransactionId])
					{
						coin.Transaction.Coins.Add(transactionCoin);
					}

					Transactions.Add(coin.Transaction);

					existingTransactions.Add(coin.TransactionId);
				}

				if (coin.SpenderTransactionId is { } && coin.SpenderTransaction is { })
				{
					if (!existingTransactions.Contains(coin.SpenderTransactionId))
					{
						foreach (var spenderCoin in transactionsDict[coin.SpenderTransactionId])
						{
							coin.SpenderTransaction.Coins.Add(spenderCoin);
						}

						Transactions.Add(coin.SpenderTransaction);

						existingTransactions.Add(coin.SpenderTransactionId);
					}
				}
			}
		}

		if (selectedCoin is { })
		{
			var coin = Coins.FirstOrDefault(x => x.TransactionId == selectedCoin.TransactionId);
			if (coin is { })
			{
				SelectedCoin = coin;
			}
		}

		if (selectedTransaction is { })
		{
			var transaction = Transactions.FirstOrDefault(x => x.TransactionId == selectedTransaction.TransactionId);
			if (transaction is { })
			{
				SelectedTransaction = transaction;
			}
		}
	}

	public string WalletName { get; private set; }

	private Dictionary<uint256, List<DebugCoinViewModel>> MapTransactions()
	{
		var transactionsDict = new Dictionary<uint256, List<DebugCoinViewModel>>();

		foreach (var coin in Coins)
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
}
