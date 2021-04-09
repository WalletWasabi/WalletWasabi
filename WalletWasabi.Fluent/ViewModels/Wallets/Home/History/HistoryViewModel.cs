using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	public partial class HistoryViewModel
	{
		private readonly Wallet _wallet;
		private readonly BitcoinStore _bitcoinStore;
		private readonly ReadOnlyObservableCollection<HistoryItemViewModel> _transactions;
		private readonly SourceList<HistoryItemViewModel> _transactionSourceList;

		[AutoNotify] private bool _showCoinJoin;

		public HistoryViewModel(Wallet wallet)
		{
			_wallet = wallet;
			_bitcoinStore = wallet.BitcoinStore;
			_transactionSourceList = new SourceList<HistoryItemViewModel>();

			var coinJoinFilter = this.WhenAnyValue(x => x.ShowCoinJoin).Select(CoinJoinFilter);

			_transactionSourceList
				.Connect()
				.Filter(coinJoinFilter)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Sort(SortExpressionComparer<HistoryItemViewModel>.Descending(x => x.Date))
				.Bind(out _transactions)
				.Subscribe();
		}

		public ReadOnlyObservableCollection<HistoryItemViewModel> Transactions => _transactions;

		private Func<HistoryItemViewModel, bool> CoinJoinFilter(bool showCoinJoin)
		{
			return item =>
			{
				if (showCoinJoin)
				{
					return true;
				}

				return !item.IsCoinJoin;
			};
		}

		public async Task UpdateAsync()
		{
			try
			{
				var historyBuilder = new TransactionHistoryBuilder(_wallet);
				var txRecordList = await Task.Run(historyBuilder.BuildHistorySummary);

				_transactionSourceList.Clear();

				Money balance = Money.Zero;
				foreach (var transactionSummary in txRecordList)
				{
					balance += transactionSummary.Amount;
					_transactionSourceList.Add(new HistoryItemViewModel(transactionSummary, _bitcoinStore, balance));
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}
}
