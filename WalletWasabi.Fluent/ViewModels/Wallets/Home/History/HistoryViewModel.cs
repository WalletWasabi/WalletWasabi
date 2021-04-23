using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui;
using WalletWasabi.Logging;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	[NavigationMetaData(Title = "Transaction History")]
	public partial class HistoryViewModel : RoutableViewModel
	{
		private readonly Wallet _wallet;
		private readonly BitcoinStore _bitcoinStore;
		private readonly ReadOnlyObservableCollection<HistoryItemViewModel> _transactions;
		private readonly SourceList<HistoryItemViewModel> _transactionSourceList;

		[AutoNotify] private bool _showCoinJoin;
		[AutoNotify] private HistoryItemViewModel? _selectedItem;

		public HistoryViewModel(Wallet wallet, UiConfig uiConfig, IObservable<Unit> updateTrigger)
		{
			_wallet = wallet;
			_bitcoinStore = wallet.BitcoinStore;
			_showCoinJoin = uiConfig.ShowCoinJoinInHistory;
			_transactionSourceList = new SourceList<HistoryItemViewModel>();

			var coinJoinFilter = this.WhenAnyValue(x => x.ShowCoinJoin).Select(CoinJoinFilter);

			_transactionSourceList
				.Connect()
				.Filter(coinJoinFilter)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Sort(SortExpressionComparer<HistoryItemViewModel>.Descending(x => x.OrderIndex))
				.Bind(out _transactions)
				.Subscribe();

			this.WhenAnyValue(x => x.ShowCoinJoin)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(showCoinJoin => uiConfig.ShowCoinJoinInHistory = showCoinJoin);

			this.WhenAnyValue(x => x.SelectedItem)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async selectedItem =>
				{
					if (selectedItem is null)
					{
						return;
					}

					Navigate(NavigationTarget.DialogScreen).To(new TransactionDetailsViewModel(selectedItem.TransactionSummary, _bitcoinStore, wallet, updateTrigger));

					Dispatcher.UIThread.Post(() =>
					{
						SelectedItem = null;
					});
				});

			updateTrigger.Subscribe(async _ => await UpdateAsync());
			RxApp.MainThreadScheduler.Schedule(async () => await UpdateAsync());
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
				for (var i = 0; i < txRecordList.Count; i++)
				{
					var transactionSummary = txRecordList[i];
					balance += transactionSummary.Amount;
					_transactionSourceList.Add(new HistoryItemViewModel(i, transactionSummary, _bitcoinStore, balance));
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}
}
