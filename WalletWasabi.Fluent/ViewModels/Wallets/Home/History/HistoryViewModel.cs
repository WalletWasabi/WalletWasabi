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

		[AutoNotify] private HistoryItemViewModel? _selectedItem;
		[AutoNotify] private bool _isWalletEmpty;

		public HistoryViewModel(WalletViewModel walletViewModel, IObservable<Unit> updateTrigger)
		{
			_walletViewModel = walletViewModel;
			_updateTrigger = updateTrigger;
			_transactionSourceList = new SourceList<HistoryItemViewModel>();
			_transactions = new ObservableCollectionExtended<HistoryItemViewModel>();
			_unfilteredTransactions = new ObservableCollectionExtended<HistoryItemViewModel>();

			this.WhenAnyValue(x => x.UnfilteredTransactions.Count)
				.Subscribe(x => IsWalletEmpty = x <= 0);

			var sortDescription = DataGridSortDescription.FromPath(nameof(HistoryItemViewModel.OrderIndex), ListSortDirection.Descending);
			CollectionView = new DataGridCollectionView(Transactions);
			CollectionView.SortDescriptions.Add(sortDescription);

			_transactionSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.Sort(SortExpressionComparer<HistoryItemViewModel>.Descending(x => x.OrderIndex))
				.Bind(_unfilteredTransactions)
				.Bind(_transactions)
				.Subscribe();
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
