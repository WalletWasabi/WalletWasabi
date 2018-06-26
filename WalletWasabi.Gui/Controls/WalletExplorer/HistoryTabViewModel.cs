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

		public HistoryTabViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel)
		{
			Transactions = new ObservableCollection<TransactionViewModel>();
			RewriteTable();

			Global.WalletService.NewBlockProcessed += WalletService_NewBlockProcessed;
			Global.WalletService.Coins.CollectionChanged += Coins_CollectionChanged;

			this.WhenAnyValue(x => x.SelectedTransaction).Subscribe(async address =>
			{
				if (address != null)
				{
					await Application.Current.Clipboard.SetTextAsync(address.TransactionId);
					ClipboardNotificationVisible = true;
					ClipboardNotificationOpacity = 1;

					Dispatcher.UIThread.Post(async () =>
					{
						await Task.Delay(1000);
						ClipboardNotificationOpacity = 0;
					});
				}
			});
		}

		private void RewriteTable()
		{
			Transactions?.Clear();

			foreach (SmartCoin coin in Global.WalletService.Coins)
			{
				Transactions.Add(new TransactionViewModel(new TransactionInfo
				{
					AmountBtc = $"+{coin.Amount.ToString(false, true)}",
					Confirmed = coin.Confirmed,
					Label = coin.Label,
					TransactionId = coin.TransactionId.ToString()
				}));

				if (coin.SpenderTransactionId != null)
				{
					Transactions.Add(new TransactionViewModel(new TransactionInfo
					{
						AmountBtc = $"-{coin.Amount.ToString(false, true)}",
						Confirmed = coin.Confirmed,
						Label = coin.Label,
						TransactionId = coin.SpenderTransactionId.ToString()
					}));
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
