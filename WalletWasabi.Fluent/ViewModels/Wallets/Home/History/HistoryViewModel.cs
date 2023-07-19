using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;
using WalletWasabi.Fluent.Views.Wallets.Home.History.Columns;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

public partial class HistoryViewModel : ActivatableViewModel
{
	private readonly SourceList<HistoryItemViewModelBase> _transactionSourceList;
	private readonly WalletViewModel _walletVm;
	private readonly ObservableCollectionExtended<HistoryItemViewModelBase> _transactions;
	private readonly ObservableCollectionExtended<HistoryItemViewModelBase> _unfilteredTransactions;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isTransactionHistoryEmpty;

	private HistoryViewModel(WalletViewModel walletVm)
	{
		_walletVm = walletVm;
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
				IndicatorsColumn(),
				DateColumn(),
				LabelsColumn(),
				IncomingColumn(),
				OutgoingColumn(),
				BalanceColumn(),
			}
		};

		Source.RowSelection!.SingleSelect = true;
	}

	public ObservableCollection<HistoryItemViewModelBase> UnfilteredTransactions => _unfilteredTransactions;

	public ObservableCollection<HistoryItemViewModelBase> Transactions => _transactions;

	public HierarchicalTreeDataGridSource<HistoryItemViewModelBase> Source { get; }

	private static IColumn<HistoryItemViewModelBase> IndicatorsColumn()
	{
		return new HierarchicalExpanderColumn<HistoryItemViewModelBase>(
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
			x => x.HasChildren(),
			x => x.IsExpanded);
	}

	private static IColumn<HistoryItemViewModelBase> DateColumn()
	{
		return new PrivacyTextColumn<HistoryItemViewModelBase>(
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
			numberOfPrivacyChars: 15);
	}

	private static IColumn<HistoryItemViewModelBase> LabelsColumn()
	{
		return new TemplateColumn<HistoryItemViewModelBase>(
			"Labels",
			new FuncDataTemplate<HistoryItemViewModelBase>((node, ns) => new LabelsColumnView(), true),
			options: new ColumnOptions<HistoryItemViewModelBase>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.Labels, LabelsArrayComparer.OrdinalIgnoreCase),
				CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.Labels, LabelsArrayComparer.OrdinalIgnoreCase),
				MinWidth = new GridLength(100, GridUnitType.Pixel)
			},
			width: new GridLength(1, GridUnitType.Star));
	}

	private static IColumn<HistoryItemViewModelBase> IncomingColumn()
	{
		return new PrivacyTextColumn<HistoryItemViewModelBase>(
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
			numberOfPrivacyChars: 9);
	}

	private static IColumn<HistoryItemViewModelBase> OutgoingColumn()
	{
		return new PrivacyTextColumn<HistoryItemViewModelBase>(
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
			numberOfPrivacyChars: 9);
	}

	private static IColumn<HistoryItemViewModelBase> BalanceColumn()
	{
		return new PrivacyTextColumn<HistoryItemViewModelBase>(
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
			numberOfPrivacyChars: 9);
	}

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

		if (txnItem is { } && Source.RowSelection is { } selection)
		{
			// Clear the selection so re-selection will work.
			Dispatcher.UIThread.Post(() => selection.Clear());

			// TDG has a visual glitch, if the item is not visible in the list, it will be glitched when gets expanded.
			// Selecting first the root item, then the child solves the issue.
			var index = _transactions.IndexOf(txnItem);
			Dispatcher.UIThread.Post(() => selection.SelectedIndex = new IndexPath(index));

			if (txnItem is CoinJoinsHistoryItemViewModel cjGroup &&
				cjGroup.Children.FirstOrDefault(x => x.Id == txid) is { } child)
			{
				txnItem.IsExpanded = true;
				child.IsFlashing = true;

				var childIndex = cjGroup.Children.IndexOf(child);
				Dispatcher.UIThread.Post(() => selection.SelectedIndex = new IndexPath(index, childIndex));
			}
			else
			{
				txnItem.IsFlashing = true;
			}
		}
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_walletVm.UiTriggers.TransactionsUpdateTrigger
			.DoAsync(async _ => await UpdateAsync())
			.Subscribe()
			.DisposeWith(disposables);
	}

	private async Task UpdateAsync()
	{
		try
		{
			var historyBuilder = new TransactionHistoryBuilder(_walletVm.Wallet);
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

	private IEnumerable<HistoryItemViewModelBase> GenerateHistoryList(List<TransactionSummary> summaries)
	{
		Money balance = Money.Zero;
		CoinJoinsHistoryItemViewModel? coinJoinGroup = default;

		var history = new List<HistoryItemViewModelBase>();

		for (var i = 0; i < summaries.Count; i++)
		{
			var item = summaries[i];

			balance += item.Amount;

			if (!item.IsOwnCoinjoin)
			{
				history.Add(new TransactionHistoryItemViewModel(UiContext, i, item, _walletVm, balance));
			}

			if (item.IsOwnCoinjoin)
			{
				if (coinJoinGroup is null)
				{
					coinJoinGroup = new CoinJoinsHistoryItemViewModel(UiContext, i, item, _walletVm);
				}
				else
				{
					coinJoinGroup.Add(item);
				}
			}

			if (coinJoinGroup is { } cjg &&
				((i + 1 < summaries.Count && !summaries[i + 1].IsOwnCoinjoin) || // The next item is not CJ so add the group.
				 i == summaries.Count - 1)) // There is no following item in the list so add the group.
			{
				if (cjg.CoinJoinTransactions.Count == 1)
				{
					var singleCjItem = new CoinJoinHistoryItemViewModel(UiContext, cjg.OrderIndex, cjg.CoinJoinTransactions.First(), _walletVm, balance, true);
					history.Add( singleCjItem);
				}
				else
				{
					cjg.SetBalance(balance);
					history.Add(cjg);
				}

				coinJoinGroup = null;
			}
		}

		// This second iteration is necessary to transform the flat list of speedups into actual groups.
		// We basically do this:
		// 1. identify which transactions are CPFP (parents) and their children
		// 2. Create a speed-up group with parent+children
		// 3. Remove the previously added items from the history (the no longer be there, but in the group)
		// 4. Add the group
		foreach (var summary in summaries)
		{
			if (summary.Transaction.IsCPFP)
			{
				// Group creation
				var childrenTxs = summary.Transaction.ParentsThisTxPaysFor;
				var parent = FindHistoryItem(summary, history);
				var children = new [] { parent }.Concat(childrenTxs.Select(tx => FindHistoryItem(tx, history)).ToList()).ToList();
				children.ForEach(x => x.IsChild = true);
				var speedUpGroup = new SpeedUpHistoryItemViewModel(parent.OrderIndex, summary, parent, children);
				speedUpGroup.SetBalance(parent.Balance);

				history.Add(speedUpGroup);

				// Removal
				history.RemoveMany(children);
			}
		}

		return history;
	}

	private HistoryItemViewModelBase FindHistoryItem(SmartTransaction transaction, IEnumerable<HistoryItemViewModelBase> summaries)
	{
		return summaries.Single(x => x.Id == transaction.GetHash());
	}

	private static HistoryItemViewModelBase FindHistoryItem(TransactionSummary summary, IEnumerable<HistoryItemViewModelBase> history)
	{
		return history.Single(x => x.Id == summary.TransactionId);
	}
}
