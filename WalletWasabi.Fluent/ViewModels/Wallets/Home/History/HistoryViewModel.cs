using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
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
	public partial class HistoryViewModel : ActivatableViewModel
	{
		private readonly Wallet _wallet;
		private readonly BitcoinStore _bitcoinStore;
		private readonly SourceList<HistoryItemViewModel> _transactionSourceList;
		private readonly IObservable<Unit> _updateTrigger;
		private readonly ObservableCollectionExtended<HistoryItemViewModel> _transactions;
		private readonly ObservableCollectionExtended<HistoryItemViewModel> _unfilteredTransactions;

		[AutoNotify] private bool _showCoinJoin;
		[AutoNotify] private HistoryItemViewModel? _selectedItem;

		public HistoryViewModel(INavigationStack<RoutableViewModel> dialogNavigationStack, Wallet wallet, UiConfig uiConfig, IObservable<Unit> updateTrigger)
		{
			_updateTrigger = updateTrigger;
			_wallet = wallet;
			_bitcoinStore = wallet.BitcoinStore;
			_showCoinJoin = uiConfig.ShowCoinJoinInHistory;
			_transactionSourceList = new SourceList<HistoryItemViewModel>();
			_transactions = new ObservableCollectionExtended<HistoryItemViewModel>();
			_unfilteredTransactions = new ObservableCollectionExtended<HistoryItemViewModel>();

			this.WhenAnyValue(x => x.ShowCoinJoin)
				.Subscribe(showCoinJoin => uiConfig.ShowCoinJoinInHistory = showCoinJoin);

			this.WhenAnyValue(x => x.SelectedItem)
				.Subscribe(async selectedItem =>
				{
					if (selectedItem is null)
					{
						return;
					}

					dialogNavigationStack.To(new TransactionDetailsViewModel(selectedItem.TransactionSummary, _bitcoinStore, wallet, updateTrigger));

					Dispatcher.UIThread.Post(() =>
					{
						SelectedItem = null;
					});
				});

			RxApp.MainThreadScheduler.Schedule(async () => await UpdateAsync());
		}

		public ObservableCollection<HistoryItemViewModel> UnfilteredTransactions => _unfilteredTransactions;

		public ObservableCollection<HistoryItemViewModel> Transactions => _transactions;

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			var coinJoinFilter = this.WhenAnyValue(x => x.ShowCoinJoin)
				.Select(CoinJoinFilter);

			_transactionSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.Sort(SortExpressionComparer<HistoryItemViewModel>.Descending(x => x.OrderIndex))
				.Bind(_unfilteredTransactions)
				.Filter(coinJoinFilter)
				.Bind(_transactions)
				.Subscribe()
				.DisposeWith(disposables);

			_updateTrigger
				.Subscribe(async _ => await UpdateAsync())
				.DisposeWith(disposables);
		}

		private static Func<HistoryItemViewModel, bool> CoinJoinFilter(bool showCoinJoin)
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

		private async Task UpdateAsync()
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
