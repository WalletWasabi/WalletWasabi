using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History
{
	public partial class HistoryViewModel : ActivatableViewModel
	{
		private readonly SourceList<HistoryItemViewModelBase> _transactionSourceList;
		private readonly WalletViewModel _walletViewModel;
		private readonly IObservable<Unit> _updateTrigger;
		private readonly ObservableCollectionExtended<HistoryItemViewModelBase> _transactions;
		private readonly ObservableCollectionExtended<HistoryItemViewModelBase> _unfilteredTransactions;
		private readonly object _transactionListLock = new();

		[AutoNotify] private HistoryItemViewModelBase? _selectedItem;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isTransactionHistoryEmpty;
		[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isInitialized;

		public HistoryViewModel(WalletViewModel walletViewModel, IObservable<Unit> updateTrigger)
		{
			_walletViewModel = walletViewModel;
			_updateTrigger = updateTrigger;
			_transactionSourceList = new SourceList<HistoryItemViewModelBase>();
			_transactions = new ObservableCollectionExtended<HistoryItemViewModelBase>();
			_unfilteredTransactions = new ObservableCollectionExtended<HistoryItemViewModelBase>();

			this.WhenAnyValue(x => x.UnfilteredTransactions.Count)
				.Subscribe(x => IsTransactionHistoryEmpty = x <= 0);

			_transactionSourceList
				.Connect()
				.ObserveOn(RxApp.MainThreadScheduler)
				.Sort(SortExpressionComparer<HistoryItemViewModelBase>.Descending(x => x.OrderIndex))
				.Bind(_unfilteredTransactions)
				.Bind(_transactions)
				.Subscribe();
		}

		public ObservableCollection<HistoryItemViewModelBase> UnfilteredTransactions => _unfilteredTransactions;

		public ObservableCollection<HistoryItemViewModelBase> Transactions => _transactions;

		public void SelectTransaction(uint256 txid)
		{
			var txnItem = Transactions.FirstOrDefault(x => x.Id == txid);

			if (txnItem is { })
			{
				SelectedItem = txnItem;
				SelectedItem.IsFlashing = true;
			}
		}

		protected override void OnActivated(CompositeDisposable disposables)
		{
			base.OnActivated(disposables);

			_updateTrigger
				.Subscribe(async _ => await UpdateAsync())
				.DisposeWith(disposables);
		}

		private async Task UpdateAsync()
		{
			try
			{
				var historyBuilder = new TransactionHistoryBuilder(_walletViewModel.Wallet);
				var rawHistoryList = await Task.Run(historyBuilder.BuildHistorySummary);
				var newHistoryList = GenerateHistoryList(rawHistoryList).ToArray();

				lock (_transactionListLock)
				{
					var copyList = Transactions.ToList();

					foreach (var oldItem in copyList)
					{
						if (newHistoryList.All(x => x.Id != oldItem.Id))
						{
							_transactionSourceList.Remove(oldItem);
						}
					}

					foreach (var newItem in newHistoryList)
					{
						if (_transactions.FirstOrDefault(x => x.Id == newItem.Id) is { } item)
						{
							item.Update(newItem);
						}
						else
						{
							_transactionSourceList.Add(newItem);
						}
					}

					if (!IsInitialized)
					{
						IsInitialized = true;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		private IEnumerable<HistoryItemViewModelBase> GenerateHistoryList(List<TransactionSummary> txRecordList)
		{
			Money balance = Money.Zero;
			CoinJoinsHistoryItemViewModel? coinJoinGroup = default;

			for (var i = 0; i < txRecordList.Count; i++)
			{
				var item = txRecordList[i];

				balance += item.Amount;

				if (item.IsLikelyCoinJoinOutput)
				{
					if (!item.IsConfirmed())
					{
						continue;
					}

					if (coinJoinGroup is null)
					{
						coinJoinGroup = new CoinJoinsHistoryItemViewModel(i, item);
					}
					else
					{
						coinJoinGroup.Add(item);
					}
				}
				else
				{
					if (coinJoinGroup is { } cjg)
					{
						cjg.SetBalance(balance);
						yield return cjg;
						coinJoinGroup = null;
					}

					yield return new TransactionHistoryItemViewModel(i, item, _walletViewModel, balance, _updateTrigger);
				}
			}
		}
	}
}
