using Avalonia;
using Avalonia.Threading;
using AvalonStudio.Extensibility;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

		public HistoryTabViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel)
		{
			Interlocked.Exchange(ref _disableClipboard, 0);
			Transactions = new ObservableCollection<TransactionViewModel>();
			RewriteTable();

			var coinsChanged = Observable.FromEventPattern(Global.WalletService.Coins, nameof(Global.WalletService.Coins.HashSetChanged));
			var newBlockProcessed = Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.NewBlockProcessed));
			var coinSpent = Observable.FromEventPattern(Global.WalletService, nameof(Global.WalletService.CoinSpentOrSpenderConfirmed));

			coinsChanged
				.Merge(newBlockProcessed)
				.Merge(coinSpent)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(o =>
				{
					RewriteTable();
				});

			this.WhenAnyValue(x => x.SelectedTransaction).Subscribe(async transaction =>
			{
				if (Interlocked.Read(ref _disableClipboard) == 0)
				{
					if (transaction != null)
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
		}

		private void RewriteTable()
		{
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

				if (coin.SpenderTransactionId != null)
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

			var rememberSelectedTransactionId = SelectedTransaction?.TransactionId;
			Transactions?.Clear();
			foreach (var txr in txRecordList)
			{
				var txinfo = new TransactionInfo
				{
					DateTime = txr.dateTime.ToLocalTime(),
					Confirmed = txr.height != Models.Height.MemPool && txr.height != Models.Height.Unknown,
					AmountBtc = $"{txr.amount.ToString(fplus: true, trimExcessZero: true)}",
					Label = txr.label,
					TransactionId = txr.transactionId.ToString()
				};
				Transactions.Add(new TransactionViewModel(txinfo));
			}

			if (Transactions.Count > 0 && rememberSelectedTransactionId != null)
			{
				var txToSelect = Transactions.FirstOrDefault(x => x.TransactionId == rememberSelectedTransactionId);
				if (txToSelect != default)
				{
					Interlocked.Exchange(ref _disableClipboard, 1);
					SelectedTransaction = txToSelect;
				}
			}
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
	}
}
