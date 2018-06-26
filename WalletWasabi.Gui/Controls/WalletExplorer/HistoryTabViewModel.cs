using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using WalletWasabi.Models;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class HistoryTabViewModel : WalletActionViewModel
	{
		private ObservableCollection<TransactionViewModel> _transactions;
		private TransactionViewModel _selectedTransaction;
		private WalletService WalletService { get; }

		public HistoryTabViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel)
		{
			WalletService = Global.WalletService;

			Transactions = new ObservableCollection<TransactionViewModel>();

			Height bestHeight = WalletService.IndexDownloader.BestHeight;
			foreach (SmartCoin coin in WalletService.Coins)
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

			WalletService.IndexDownloader.BestHeightChanged += IndexDownloader_BestHeightChanged;
			WalletService.Coins.CollectionChanged += Coins_CollectionChanged;
		}

		private void Coins_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			//throw new NotImplementedException();
		}

		private void IndexDownloader_BestHeightChanged(object sender, Models.Height e)
		{
			//throw new NotImplementedException();
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
