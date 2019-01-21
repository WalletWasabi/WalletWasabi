using Avalonia;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class HistoryTabViewModel : WalletActionViewModel
	{
		private ObservableCollection<TransactionViewModel> _transactions;
		private TransactionViewModel _selectedTransaction;
		private double _clipboardNotificationOpacity;
		private bool _clipboardNotificationVisible;
		private long _disableClipboard;
		private SortOrder _dateSortDirection;
		private SortOrder _amountSortDirection;
		private SortOrder _transactionSortDirection;

		public HistoryTabViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel)
		{
			Interlocked.Exchange(ref _disableClipboard, 0);
			Transactions = new ObservableCollection<TransactionViewModel>();
			RewriteTableAsync();

			var coinsChanged = Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.CollectionChanged));
			var newBlockProcessed = Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.NewBlockProcessed));
			var coinSpent = Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.CoinSpentOrSpenderConfirmed));

			coinsChanged
				.Merge(newBlockProcessed)
				.Merge(coinSpent)
				.Throttle(TimeSpan.FromSeconds(5))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					RewriteTableAsync();
				});

			this.WhenAnyValue(x => x.SelectedTransaction).Subscribe(async transaction =>
			{
				if (Interlocked.Read(ref _disableClipboard) == 0)
				{
					if (!(transaction is null))
					{
						await Application.Current.Clipboard.SetTextAsync(transaction.TransactionId);
						ClipboardNotificationVisible = true;
						ClipboardNotificationOpacity = 1;

						Dispatcher.UIThread.Post(async () =>
						{
							await Task.Delay(1000);
							ClipboardNotificationOpacity = 0;
						});
					}
				}
				else
				{
					Interlocked.Exchange(ref _disableClipboard, 0);
				}
			});
			DateSortDirection = SortOrder.Decreasing;
		}

		private async void RewriteTableAsync()
		{
			var txRecordList = await Task.Run(() =>
			{
				return BuildTxRecordList();
			});

			var rememberSelectedTransactionId = SelectedTransaction?.TransactionId;
			Transactions?.Clear();

			var trs = txRecordList.Select(txr => new TransactionInfo()
			{
				DateTime = txr.dateTime.ToLocalTime(),
				Confirmed = txr.height != WalletWasabi.Models.Height.MemPool && txr.height != WalletWasabi.Models.Height.Unknown,
				AmountBtc = $"{txr.amount.ToString(fplus: true, trimExcessZero: true)}",
				Label = txr.label,
				TransactionId = txr.transactionId.ToString()
			}).Select(ti => new TransactionViewModel(ti));

			Transactions = new ObservableCollection<TransactionViewModel>(trs);

			if (Transactions.Count > 0 && !(rememberSelectedTransactionId is null))
			{
				var txToSelect = Transactions.FirstOrDefault(x => x.TransactionId == rememberSelectedTransactionId);
				if (txToSelect != default)
				{
					Interlocked.Exchange(ref _disableClipboard, 1);
					SelectedTransaction = txToSelect;
				}
			}
			RefreshOrdering();
		}

		private static List<(DateTimeOffset dateTime, Height height, Money amount, string label, uint256 transactionId)> BuildTxRecordList()
		{
			List<Transaction> trs = new List<Transaction>();
			var txRecordList = new List<(DateTimeOffset dateTime, Height height, Money amount, string label, uint256 transactionId)>();
			foreach (SmartCoin coin in Global.WalletService.Coins)
			{
				var found = txRecordList.FirstOrDefault(x => x.transactionId == coin.TransactionId);
				SmartTransaction foundTransaction = Global.WalletService.TransactionCache.First(x => x.GetHash() == coin.TransactionId);
				DateTimeOffset dateTime;
				if (foundTransaction.Height.Type == HeightType.Chain)
				{
					if (Global.WalletService.ProcessedBlocks.Any(x => x.Value.height == foundTransaction.Height))
					{
						dateTime = Global.WalletService.ProcessedBlocks.First(x => x.Value.height == foundTransaction.Height).Value.dateTime;
					}
					else
					{
						dateTime = DateTimeOffset.UtcNow;
					}
				}
				else
				{
					dateTime = foundTransaction.FirstSeenIfMemPoolTime ?? DateTimeOffset.UtcNow;
				}
				if (found != default) // if found
				{
					txRecordList.Remove(found);
					var foundLabel = found.label != string.Empty ? found.label + ", " : "";
					var newRecord = (dateTime, found.height, found.amount + coin.Amount, $"{foundLabel}{coin.Label}", coin.TransactionId);
					txRecordList.Add(newRecord);
				}
				else
				{
					txRecordList.Add((dateTime, coin.Height, coin.Amount, coin.Label, coin.TransactionId));
				}

				if (!(coin.SpenderTransactionId is null))
				{
					SmartTransaction foundSpenderTransaction = Global.WalletService.TransactionCache.First(x => x.GetHash() == coin.SpenderTransactionId);
					if (foundSpenderTransaction.Height.Type == HeightType.Chain)
					{
						if (Global.WalletService.ProcessedBlocks.Any(x => x.Value.height == foundSpenderTransaction.Height))
						{
							dateTime = Global.WalletService.ProcessedBlocks.First(x => x.Value.height == foundSpenderTransaction.Height).Value.dateTime;
						}
						else
						{
							dateTime = DateTimeOffset.UtcNow;
						}
					}
					else
					{
						dateTime = foundSpenderTransaction.FirstSeenIfMemPoolTime ?? DateTimeOffset.UtcNow;
					}

					var foundSpenderCoin = txRecordList.FirstOrDefault(x => x.transactionId == coin.SpenderTransactionId);
					if (foundSpenderCoin != default) // if found
					{
						txRecordList.Remove(foundSpenderCoin);
						var newRecord = (dateTime, foundSpenderTransaction.Height, foundSpenderCoin.amount - coin.Amount, foundSpenderCoin.label, coin.SpenderTransactionId);
						txRecordList.Add(newRecord);
					}
					else
					{
						txRecordList.Add((dateTime, foundSpenderTransaction.Height, (Money.Zero - coin.Amount), "", coin.SpenderTransactionId));
					}
				}
			}
			txRecordList = txRecordList.OrderByDescending(x => x.dateTime).ThenBy(x => x.amount).ToList();
			return txRecordList;
		}

		public ObservableCollection<TransactionViewModel> Transactions
		{
			get { return _transactions; }
			set { this.RaiseAndSetIfChanged(ref _transactions, value); }
		}

		public TransactionViewModel SelectedTransaction
		{
			get { return _selectedTransaction; }
			set { this.RaiseAndSetIfChanged(ref _selectedTransaction, value); }
		}

		public double ClipboardNotificationOpacity
		{
			get { return _clipboardNotificationOpacity; }
			set { this.RaiseAndSetIfChanged(ref _clipboardNotificationOpacity, value); }
		}

		public bool ClipboardNotificationVisible
		{
			get { return _clipboardNotificationVisible; }
			set { this.RaiseAndSetIfChanged(ref _clipboardNotificationVisible, value); }
		}

		public SortOrder DateSortDirection
		{
			get => _dateSortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _dateSortDirection, value);
				if (value != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					TransactionSortDirection = SortOrder.None;
				}
				RefreshOrdering();
			}
		}

		public SortOrder AmountSortDirection
		{
			get => _amountSortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _amountSortDirection, value);
				if (value != SortOrder.None)
				{
					DateSortDirection = SortOrder.None;
					TransactionSortDirection = SortOrder.None;
				}
				RefreshOrdering();
			}
		}

		public SortOrder TransactionSortDirection
		{
			get => _transactionSortDirection;
			set
			{
				this.RaiseAndSetIfChanged(ref _transactionSortDirection, value);
				if (value != SortOrder.None)
				{
					AmountSortDirection = SortOrder.None;
					DateSortDirection = SortOrder.None;
				}
				RefreshOrdering();
			}
		}

		private void RefreshOrdering()
		{
			if (TransactionSortDirection != SortOrder.None)
			{
				switch (TransactionSortDirection)
				{
					case SortOrder.Increasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderBy(t => t.TransactionId));
						break;

					case SortOrder.Decreasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderByDescending(t => t.TransactionId));
						break;
				}
			}
			else if (AmountSortDirection != SortOrder.None)
			{
				switch (AmountSortDirection)
				{
					case SortOrder.Increasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderBy(t => t.Amount));
						break;

					case SortOrder.Decreasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderByDescending(t => t.Amount));
						break;
				}
			}
			else if (DateSortDirection != SortOrder.None)
			{
				switch (DateSortDirection)
				{
					case SortOrder.Increasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderBy(t => t.DateTime));
						break;

					case SortOrder.Decreasing:
						Transactions = new ObservableCollection<TransactionViewModel>(_transactions.OrderByDescending(t => t.DateTime));
						break;
				}
			}
		}
	}
}
