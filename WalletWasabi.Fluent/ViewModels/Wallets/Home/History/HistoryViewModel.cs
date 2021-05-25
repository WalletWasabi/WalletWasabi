using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	public partial class HistoryViewModel : ActivatableViewModel
	{
		private readonly SourceList<HistoryItemViewModel> _transactionSourceList;
		private readonly WalletViewModel _walletViewModel;
		private readonly IObservable<Unit> _updateTrigger;
		private readonly ObservableCollectionExtended<HistoryItemViewModel> _transactions;
		private readonly ObservableCollectionExtended<HistoryItemViewModel> _unfilteredTransactions;
		private readonly object _transactionListLock = new();

		[AutoNotify] private bool _showCoinJoin;
		[AutoNotify] private HistoryItemViewModel? _selectedItem;

		public HistoryViewModel(WalletViewModel walletViewModel, IObservable<Unit> updateTrigger)
		{
			_walletViewModel = walletViewModel;
			_updateTrigger = updateTrigger;
			_showCoinJoin = Services.UiConfig.ShowCoinJoinInHistory;
			_transactionSourceList = new SourceList<HistoryItemViewModel>();
			_transactions = new ObservableCollectionExtended<HistoryItemViewModel>();
			_unfilteredTransactions = new ObservableCollectionExtended<HistoryItemViewModel>();

			this.WhenAnyValue(x => x.ShowCoinJoin)
				.Subscribe(showCoinJoin => Services.UiConfig.ShowCoinJoinInHistory = showCoinJoin);

			var sortDescription = DataGridSortDescription.FromPath(nameof(HistoryItemViewModel.OrderIndex), ListSortDirection.Descending);
			CollectionView = new DataGridCollectionView(Transactions);
			CollectionView.SortDescriptions.Add(sortDescription);

			var coinJoinFilter = this.WhenAnyValue(x => x.ShowCoinJoin)
				.Select(CoinJoinFilter);

			_transactionSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.Sort(SortExpressionComparer<HistoryItemViewModel>.Descending(x => x.OrderIndex))
				.Bind(_unfilteredTransactions)
				.Filter(coinJoinFilter)
				.Bind(_transactions)
				.Subscribe();

			this.WhenAnyValue(x => x.SelectedItem)
				.Buffer(2, 1)
				.Select(buf => buf[0])
				.WhereNotNull()
				.Subscribe(x => x.IsSelected = false);
		}

		public DataGridCollectionView CollectionView { get; }

		public ObservableCollection<HistoryItemViewModel> UnfilteredTransactions => _unfilteredTransactions;

		public ObservableCollection<HistoryItemViewModel> Transactions => _transactions;

		public void SelectTransaction(uint256 txid)
		{
			var txnItem = Transactions.FirstOrDefault(x => x.TransactionSummary.TransactionId == txid);

			if (txnItem is { })
			{
				SelectedItem = txnItem;
				SelectedItem.IsSelected = true;

				RxApp.MainThreadScheduler.Schedule(async () =>
				{
					await Task.Delay(1260);
					SelectedItem.IsSelected = false;
				});
			}
		}

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			RxApp.MainThreadScheduler.Schedule(async () => await UpdateAsync());

			_updateTrigger
				.Subscribe(async _ => await UpdateAsync())
				.DisposeWith(disposables);

			disposables.Add(Disposable.Create(() => _transactionSourceList.Clear()));
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
				var historyBuilder = new TransactionHistoryBuilder(_walletViewModel.Wallet);
				var txRecordList = await Task.Run(historyBuilder.BuildHistorySummary);

				lock (_transactionListLock)
				{
					_transactionSourceList.Clear();

					Money balance = Money.Zero;
					for (var i = 0; i < txRecordList.Count; i++)
					{
						var transactionSummary = txRecordList[i];
						balance += transactionSummary.Amount;
						_transactionSourceList.Add(new HistoryItemViewModel(i, transactionSummary, _walletViewModel, balance, _updateTrigger));
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}
	}
}
