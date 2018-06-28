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

			Global.WalletService.NewBlockProcessed += WalletService_NewBlockProcessed;
			Global.WalletService.Coins.CollectionChanged += Coins_CollectionChanged;
			Global.WalletService.CoinSpent += WalletService_CoinSpent;

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
			var txRecordList = new List<(bool confirmed, Money amount, string label, uint256 transactionId)>();
			foreach (SmartCoin coin in Global.WalletService.Coins.OrderByDescending(x => x.Height))
			{
				var found = txRecordList.FirstOrDefault(x => x.transactionId == coin.TransactionId);
				if (found != default) // if found
				{
					txRecordList.Remove(found);
					var newRecord = (found.confirmed, found.amount + coin.Amount, $"{found.label}, {coin.Label}", coin.TransactionId);
					txRecordList.Add(newRecord);
				}
				else
				{
					txRecordList.Add((coin.Confirmed, coin.Amount, coin.Label, coin.TransactionId));
				}

				if (coin.SpenderTransactionId != null)
				{
					bool guessConfirmed = !Global.MemPoolService.TransactionHashes.Contains(coin.SpenderTransactionId) && coin.Confirmed; // If it's not in the mempool it's likely confirmed && coin is confirmed.

					var foundSpender = txRecordList.FirstOrDefault(x => x.transactionId == coin.SpenderTransactionId);
					if (foundSpender != default) // if found
					{
						txRecordList.Remove(foundSpender);
						var newRecord = (guessConfirmed, foundSpender.amount - coin.Amount, foundSpender.label, coin.SpenderTransactionId);
						txRecordList.Add(newRecord);
					}
					else
					{
						txRecordList.Add((guessConfirmed, (Money.Zero - coin.Amount), "", coin.SpenderTransactionId));
					}
				}
			}

			txRecordList = txRecordList.OrderBy(x => x.confirmed).ToList();

			var rememberSelectedTransactionId = SelectedTransaction?.TransactionId;
			Transactions?.Clear();
			foreach (var txr in txRecordList)
			{
				var txinfo = new TransactionInfo
				{
					Confirmed = txr.confirmed,
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

		private void WalletService_NewBlockProcessed(object sender, Block e)
		{
			Dispatcher.UIThread.InvokeAsync(() => RewriteTable());
		}

		private void Coins_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			Dispatcher.UIThread.InvokeAsync(() => RewriteTable());
		}

		private void WalletService_CoinSpent(object sender, SmartCoin e)
		{
			Dispatcher.UIThread.InvokeAsync(() => RewriteTable());
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
