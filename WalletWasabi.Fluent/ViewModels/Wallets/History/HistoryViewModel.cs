using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.History
{
	public partial class HistoryViewModel
	{
		private readonly Wallet _wallet;
		private readonly BitcoinStore _bitcoinStore;

		[AutoNotify] private ObservableCollection<HistoryItemViewModel> _transactions;
		[AutoNotify] private bool _showCoinJoin;

		public HistoryViewModel(Wallet wallet)
		{
			_wallet = wallet;
			_bitcoinStore = wallet.BitcoinStore;
			_transactions = new ObservableCollection<HistoryItemViewModel>();

			this.WhenAnyValue(x => x.ShowCoinJoin)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async _ => await UpdateAsync());
		}

		public async Task UpdateAsync()
		{
			try
			{
				var historyBuilder = new TransactionHistoryBuilder(_wallet);
				var txRecordList = await Task.Run(historyBuilder.BuildHistorySummary);

				Transactions.Clear();
				var trs = txRecordList.Select(transactionSummary => new HistoryItemViewModel(transactionSummary, _bitcoinStore));

				if (!ShowCoinJoin)
				{
					trs = trs.Where(x => !x.IsCoinJoin);
				}

				Transactions.AddRange(trs.Reverse());
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}
}
