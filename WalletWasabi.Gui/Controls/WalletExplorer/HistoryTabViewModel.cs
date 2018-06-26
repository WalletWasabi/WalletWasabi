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
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class HistoryTabViewModel : WalletActionViewModel
	{
		private ObservableCollection<TransactionViewModel> _transactions;
		private TransactionViewModel _selectedTransaction;

		public HistoryTabViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel)
		{
			Transactions = new ObservableCollection<TransactionViewModel>();
			RewriteTable();

			Global.WalletService.NewBlockProcessed += WalletService_NewBlockProcessed;
			Global.WalletService.Coins.CollectionChanged += Coins_CollectionChanged;
		}

		private void RewriteTable()
		{
			var txRecordList = new List<(bool confirmed, Money amount, string label, uint256 transactionId)>();
			foreach (SmartCoin coin in Global.WalletService.Coins.OrderByDescending(x => x.Height))
			{
				var found = txRecordList.FirstOrDefault(x => x.transactionId == coin.TransactionId);
				if (found != default)
				{
					txRecordList.Remove(found);
					var newRecord = (coin.Confirmed, found.amount + coin.Amount, $"{found.label}, {coin.Label}", coin.TransactionId);
					txRecordList.Add(newRecord);
				}
				else
				{
					txRecordList.Add((coin.Confirmed, coin.Amount, coin.Label, coin.TransactionId));
				}

				if (coin.SpenderTransactionId != null)
				{
					txRecordList.Add((coin.Confirmed, (Money.Zero - coin.Amount), coin.Label, coin.TransactionId));
				}
			}

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
		}

		private void WalletService_NewBlockProcessed(object sender, Block e)
		{
			Dispatcher.UIThread.InvokeAsync(() => RewriteTable());
		}

		private void Coins_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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
	}
}
