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
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
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

	[AutoNotify] private HistoryItemViewModelBase? _selectedItem;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isTransactionHistoryEmpty;

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
			.Sort(SortExpressionComparer<HistoryItemViewModelBase>
				.Ascending(x => x.IsConfirmed)
				.ThenByDescending(x => x.OrderIndex))
			.Bind(_unfilteredTransactions)
			.Bind(_transactions)
			.Subscribe();

		// [Column]			[View]						[Header]		[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Indicators		IndicatorsColumnView		-				Auto		80				-			true
		// Date				DateColumnView				Date / Time		Auto		150				-			true
		// Labels			LabelsColumnView			Labels			*			75				-			true
		// Incoming			IncomingColumnView			Incoming (BTC)	Auto		145				210			true
		// Outgoing			OutgoingColumnView			Outgoing (BTC)	Auto		145				210			true
		// Balance			BalanceColumnView			Balance (BTC)	Auto		145				210			true

		// NOTE: When changing column width or min width please also change HistoryPlaceholderPanel column widths.

		Source = new HierarchicalTreeDataGridSource<HistoryItemViewModelBase>(_transactions)
		{
			Columns =
			{
				// Indicators
				new HierarchicalExpanderColumn<HistoryItemViewModelBase>(
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
				x => x.Children,
				x =>
				{
					if (x is CoinJoinsHistoryItemViewModel coinJoinsHistoryItemViewModel
					    && coinJoinsHistoryItemViewModel.CoinJoinTransactions.Count > 1)
					{
						return true;
					}

					return false;
				},
				x => x.IsExpanded),

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
						CanUserSortColumn = true,
						CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.Label),
						CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.Label),
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
						MinWidth = new GridLength(145, GridUnitType.Pixel),
						MaxWidth = new GridLength(210, GridUnitType.Pixel)
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
						MinWidth = new GridLength(145, GridUnitType.Pixel),
						MaxWidth = new GridLength(210, GridUnitType.Pixel)
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
						MinWidth = new GridLength(145, GridUnitType.Pixel),
						MaxWidth = new GridLength(210, GridUnitType.Pixel)
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

	public HierarchicalTreeDataGridSource<HistoryItemViewModelBase> Source { get; }

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
			Dispatcher.UIThread.Post(() =>
			{
				Source.RowSelection!.SelectedIndex = new IndexPath(index);
			});
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
			var orderedRawHistoryList = rawHistoryList.OrderBy(x => x.DateTime).ThenBy(x => x.Height).ThenBy(x => x.BlockIndex).ToList();
			var newHistoryList = GenerateHistoryList(orderedRawHistoryList).ToArray();

			_transactionSourceList.Edit(x =>
			{
				x.Clear();
				x.AddRange(newHistoryList);
			});
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
					coinJoinGroup = new CoinJoinsHistoryItemViewModel(i, item, _walletViewModel, _updateTrigger);
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
				if (cjg.CoinJoinTransactions.Count == 1)
				{
					var singleCjItem = new CoinJoinHistoryItemViewModel(cjg.OrderIndex, cjg.CoinJoinTransactions.First(), _walletViewModel, balance, _updateTrigger, true);
					yield return singleCjItem;
				}
				else
				{
					cjg.SetBalance(balance);
					yield return cjg;
				}

				coinJoinGroup = null;
			}
		}
	}
}
