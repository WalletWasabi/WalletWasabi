using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;
using WalletWasabi.Fluent.Views.Wallets.Home.History.Columns;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

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

		// [Column]			[View]						[Header]		[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Indicators		IndicatorsColumnView		-				Auto		80				-			false
		// Date				DateColumnView				Date / Time		Auto		150				-			true
		// Labels			LabelsColumnView			Labels			*			75				-			false
		// Incoming			IncomingColumnView			Incoming (BTC)	Auto		130				150			true
		// Outgoing			OutgoingColumnView			Outgoing (BTC)	Auto		130				150			true
		// Balance			BalanceColumnView			Balance (BTC)		Auto		130				150			true

		Source = new FlatTreeDataGridSource<HistoryItemViewModelBase>(_transactions)
		{
			Columns =
			{
				// Indicators
				new TemplateColumn<HistoryItemViewModelBase>(
					null,
					new FuncDataTemplate<HistoryItemViewModelBase>((node, ns) => new IndicatorsColumnView(), true),
					options: new ColumnOptions<HistoryItemViewModelBase>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.IsCoinJoin),
						CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.IsCoinJoin),
						MinWidth = new GridLength(80, GridUnitType.Pixel)
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// Date
				new PrivacyTextColumn<HistoryItemViewModelBase>(
					"Date / Time",
					x => x.DateString,
					options: new ColumnOptions<HistoryItemViewModelBase>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.Date),
						CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.Date),
						MinWidth = new GridLength(150, GridUnitType.Pixel)
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 15),

				// Labels
				new TemplateColumn<HistoryItemViewModelBase>(
					"Labels",
					new FuncDataTemplate<HistoryItemViewModelBase>((node, ns) => new LabelsColumnView(), true),
					options: new ColumnOptions<HistoryItemViewModelBase>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = false,
						MinWidth = new GridLength(100, GridUnitType.Pixel)
					},
					width: new GridLength(1, GridUnitType.Star)),

				// Incoming
				new PrivacyTextColumn<HistoryItemViewModelBase>(
					"Incoming (BTC)",
					x => x.IncomingAmount?.ToFormattedString(),
					options: new ColumnOptions<HistoryItemViewModelBase>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.IncomingAmount),
						CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.IncomingAmount),
						MinWidth = new GridLength(130, GridUnitType.Pixel),
						MaxWidth = new GridLength(150, GridUnitType.Pixel)
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// Outgoing
				new PrivacyTextColumn<HistoryItemViewModelBase>(
					"Outgoing (BTC)",
					x => x.OutgoingAmount?.ToFormattedString(),
					options: new ColumnOptions<HistoryItemViewModelBase>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.OutgoingAmount),
						CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.OutgoingAmount),
						MinWidth = new GridLength(130, GridUnitType.Pixel),
						MaxWidth = new GridLength(150, GridUnitType.Pixel)
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// Balance
				new PrivacyTextColumn<HistoryItemViewModelBase>(
					"Balance (BTC)",
					x => x.Balance?.ToFormattedString(),
					options: new ColumnOptions<HistoryItemViewModelBase>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.Balance),
						CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.Balance),
						MinWidth = new GridLength(130, GridUnitType.Pixel),
						MaxWidth = new GridLength(150, GridUnitType.Pixel)
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),
			}
		};

		Source.RowSelection!.SingleSelect = true;

		Source.RowSelection
			.WhenAnyValue(x => x.SelectedItem)
			.Subscribe(x => SelectedItem = x);
	}

	public ObservableCollection<HistoryItemViewModelBase> UnfilteredTransactions => _unfilteredTransactions;

	public ObservableCollection<HistoryItemViewModelBase> Transactions => _transactions;

	public FlatTreeDataGridSource<HistoryItemViewModelBase> Source { get; }

	public void SelectTransaction(uint256 txid)
	{
		var txnItem = Transactions.FirstOrDefault(item =>
		{
			if (item is CoinJoinsHistoryItemViewModel cjGroup)
			{
				return cjGroup.CoinJoinTransactions.Any(x => x.TransactionId == txid);
			}

			return item.Id == txid;
		});

		if (txnItem is { })
		{
			SelectedItem = txnItem;
			SelectedItem.IsFlashing = true;

			var index = _transactions.IndexOf(SelectedItem);
			Source.RowSelection!.SelectedIndex = new IndexPath(index);
		}
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_updateTrigger
			.SubscribeAsync(async _ => await UpdateAsync())
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
				_transactionSourceList.Edit(x =>
				{
					var copyList = Transactions.ToList();

					foreach (var oldItem in copyList)
					{
						if (newHistoryList.All(x => x.Id != oldItem.Id))
						{
							x.Remove(oldItem);
						}
					}

					foreach (var newItem in newHistoryList)
					{
						if (_transactions.FirstOrDefault(x => x.Id == newItem.Id) is { } item)
						{
							if (item.GetType() != newItem.GetType())
							{
								x.Remove(item);
								x.Add(newItem);
							}
							else
							{
								item.Update(newItem);
							}
						}
						else
						{
							x.Add(newItem);
						}
					}
				});

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

			if (!item.IsOwnCoinjoin)
			{
				yield return new TransactionHistoryItemViewModel(i, item, _walletViewModel, balance, _updateTrigger);
			}

			if (item.IsOwnCoinjoin)
			{
				if (coinJoinGroup is null)
				{
					coinJoinGroup = new CoinJoinsHistoryItemViewModel(i, item);
				}
				else
				{
					coinJoinGroup.Add(item);
				}
			}

			if (coinJoinGroup is { } cjg &&
				(i + 1 < txRecordList.Count && !txRecordList[i + 1].IsOwnCoinjoin || // The next item is not CJ so add the group.
				 i == txRecordList.Count - 1)) // There is no following item in the list so add the group.
			{
				cjg.SetBalance(balance);
				yield return cjg;
				coinJoinGroup = null;
			}
		}
	}
}
