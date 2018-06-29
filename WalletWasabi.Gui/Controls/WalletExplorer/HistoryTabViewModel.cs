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
			var txRecordList = new List<(Height height, Money amount, string label, uint256 transactionId)>();
			foreach (SmartCoin coin in Global.WalletService.Coins)
			{
				var found = txRecordList.FirstOrDefault(x => x.transactionId == coin.TransactionId);
				if (found != default) // if found
				{
					txRecordList.Remove(found);
					var foundLabel = found.label != string.Empty ? found.label + ", " : "";
					var newRecord = (found.height, found.amount + coin.Amount, $"{foundLabel}{coin.Label}", coin.TransactionId);
					txRecordList.Add(newRecord);
				}
				else
				{
					txRecordList.Add((coin.Height, coin.Amount, coin.Label, coin.TransactionId));
				}

				if (coin.SpenderTransactionId != null)
				{
					bool guessConfirmed = !Global.MemPoolService.TransactionHashes.Contains(coin.SpenderTransactionId) && coin.Confirmed; // If it's not in the mempool it's likely confirmed && coin is confirmed.
					var guessHeight = guessConfirmed ? coin.Height : Models.Height.MemPool;

					var foundSpender = txRecordList.FirstOrDefault(x => x.transactionId == coin.SpenderTransactionId);
					if (foundSpender != default) // if found
					{
						txRecordList.Remove(foundSpender);
						guessHeight = Math.Max(guessHeight, foundSpender.height);
						var newRecord = (guessHeight, foundSpender.amount - coin.Amount, foundSpender.label, coin.SpenderTransactionId);
						txRecordList.Add(newRecord);
					}
					else
					{
						txRecordList.Add((guessHeight, (Money.Zero - coin.Amount), "", coin.SpenderTransactionId));
					}
				}
			}

			txRecordList = txRecordList.OrderByDescending(x => x.height).ToList();

			var rememberSelectedTransactionId = SelectedTransaction?.TransactionId;
			Transactions?.Clear();
			foreach (var txr in txRecordList)
			{
				var txinfo = new TransactionInfo
				{
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
