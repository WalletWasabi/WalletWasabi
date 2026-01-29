using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Controls.Sorting;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;
using WalletWasabi.Fluent.Views.Wallets.Home.History.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

[AppLifetime]
public partial class HistoryViewModel : ActivatableViewModel
{
	private readonly IWalletModel _wallet;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private HierarchicalTreeDataGridSource<HistoryItemViewModelBase>? _source; // This will get its value as soon as this VM is activated.

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _isTransactionHistoryEmpty;

	private HistoryViewModel(IWalletModel wallet)
	{
		_wallet = wallet;

		_wallet.Transactions.Cache.Connect()
			.Transform(x => CreateViewModel(x))
			.Sort(
				SortExpressionComparer<HistoryItemViewModelBase>
					.Ascending(x => x.Transaction.IsConfirmed)
					.ThenByDescending(x => x.Transaction.OrderIndex))
			.Bind(Transactions)
		.Subscribe();

		_wallet.Transactions.IsEmpty
			.BindTo(this, x => x.IsTransactionHistoryEmpty);
	}

	public IObservableCollection<HistoryItemViewModelBase> Transactions { get; } = new ObservableCollectionExtended<HistoryItemViewModelBase>();

	public IEnumerable<SortableItem> Sortables { get; private set; }

	private static IColumn<HistoryItemViewModelBase> IndicatorsColumn()
	{
		return new HierarchicalExpanderColumn<HistoryItemViewModelBase>(
			new TemplateColumn<HistoryItemViewModelBase>(
				null,
				new FuncDataTemplate<HistoryItemViewModelBase>((node, ns) => new IndicatorsColumnView(), true),
				null,
				options: new TemplateColumnOptions<HistoryItemViewModelBase>
				{
					CanUserResizeColumn = false,
					CanUserSortColumn = true,
					CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.Transaction.IsCoinjoin),
					CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.Transaction.IsCoinjoin),
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
			x => x.Transaction.DateString,
			type: PrivacyCellType.Date,
			options: new ColumnOptions<HistoryItemViewModelBase>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.Transaction.Date),
				CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.Transaction.Date),
			},
			width: new GridLength(0, GridUnitType.Auto),
			numberOfPrivacyChars: 8);
	}

	private static IColumn<HistoryItemViewModelBase> LabelsColumn()
	{
		return new TemplateColumn<HistoryItemViewModelBase>(
			"Labels",
			new FuncDataTemplate<HistoryItemViewModelBase>((node, ns) => new LabelsColumnView(), true),
			null,
			options: new TemplateColumnOptions<HistoryItemViewModelBase>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.Transaction.Labels, LabelsArrayComparer.OrdinalIgnoreCase),
				CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.Transaction.Labels, LabelsArrayComparer.OrdinalIgnoreCase),
				MinWidth = new GridLength(100, GridUnitType.Pixel)
			},
			width: new GridLength(1, GridUnitType.Star));
	}

	private static IColumn<HistoryItemViewModelBase> AmountColumn()
	{
		return new TemplateColumn<HistoryItemViewModelBase>(
			null,
			new FuncDataTemplate<HistoryItemViewModelBase>((x, _) => new AmountColumnView(), true),
			null,
			new GridLength(170, GridUnitType.Pixel),
			new TemplateColumnOptions<HistoryItemViewModelBase>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = HistoryItemViewModelBase.SortAscending(x => x.Transaction.Amount),
				CompareDescending = HistoryItemViewModelBase.SortDescending(x => x.Transaction.Amount),
			});
	}

	private IColumn<HistoryItemViewModelBase> ActionsColumn()
	{
		return new TemplateColumn<HistoryItemViewModelBase>(
			"Actions",
			new FuncDataTemplate<HistoryItemViewModelBase>((node, ns) => new ActionsColumnView(), true),
			options: new TemplateColumnOptions<HistoryItemViewModelBase>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = false,
			},
			width: new GridLength(0, GridUnitType.Auto));
	}

	public void SelectTransaction(uint256 txid)
	{
		var transactionsSnapshot = Transactions.ToArray();
		var txnItem = transactionsSnapshot.FirstOrDefault(item =>
		{
			if (item is CoinJoinsHistoryItemViewModel cjGroup)
			{
				return cjGroup.Children.Any(x => x.Transaction.Id == txid);
			}

			return item.Transaction.Id == txid;
		});

		if (txnItem is { } && Source?.RowSelection is { } selection)
		{
			// Clear the selection so re-selection will work.
			Dispatcher.UIThread.Post(() => selection.Clear());

			// TDG has a visual glitch, if the item is not visible in the list, it will be glitched when gets expanded.
			// Selecting first the root item, then the child solves the issue.
			var index = transactionsSnapshot.IndexOf(txnItem);
			Dispatcher.UIThread.Post(() => selection.SelectedIndex = new IndexPath(index));

			if (txnItem is CoinJoinsHistoryItemViewModel cjGroup &&
			    cjGroup.Children.FirstOrDefault(x => x.Transaction.Id == txid) is { } child)
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

		// [Column]			[View]						[Header]		[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Indicators		IndicatorsColumnView		-				Auto		80				-			true
		// Date				DateColumnView				Date / Time		Auto		150				-			true
		// Labels			LabelsColumnView			Labels			*			75				-			true
		// Received			ReceivedColumnView			Received (BTC)	Auto		145				210			true
		// Sent				SentColumnView				Sent (BTC)		Auto		145				210			true
		// Balance			BalanceColumnView			Balance (BTC)	Auto		145				210			true

		// NOTE: When changing column width or min width please also change HistoryPlaceholderPanel column widths.
		Source = new HierarchicalTreeDataGridSource<HistoryItemViewModelBase>(Transactions)
		{
			Columns =
			{
				IndicatorsColumn(),
				DateColumn(),
				AmountColumn(),
				LabelsColumn(),
				ActionsColumn(),
			}
		}.DisposeWith(disposables);

		Source.RowSelection!.SingleSelect = true;

		Sortables =
		[
			new SortableItem("Status") { SortByAscendingCommand = ReactiveCommand.Create(() => Source!.SortBy(Source.Columns[0], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => Source!.SortBy(Source.Columns[0], ListSortDirection.Descending)) },
			new SortableItem("Date") { SortByAscendingCommand = ReactiveCommand.Create(() => Source!.SortBy(Source.Columns[1], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => Source!.SortBy(Source.Columns[1], ListSortDirection.Descending)) },
			new SortableItem("Amount") { SortByAscendingCommand = ReactiveCommand.Create(() => Source!.SortBy(Source.Columns[2], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => Source!.SortBy(Source.Columns[2], ListSortDirection.Descending)) },
			new SortableItem("Label") { SortByAscendingCommand = ReactiveCommand.Create(() => Source!.SortBy(Source.Columns[3], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => Source!.SortBy(Source.Columns[3], ListSortDirection.Descending)) },
		];
	}

	private HistoryItemViewModelBase CreateViewModel(TransactionModel transaction, HistoryItemViewModelBase? parent = null)
	{
		HistoryItemViewModelBase viewModel = transaction.Type switch
		{
			TransactionType.IncomingTransaction => new TransactionHistoryItemViewModel(UiContext, _wallet, transaction),
			TransactionType.OutgoingTransaction => new TransactionHistoryItemViewModel(UiContext, _wallet, transaction),
			TransactionType.SelfTransferTransaction => new TransactionHistoryItemViewModel(UiContext, _wallet, transaction),
			TransactionType.Coinjoin => new CoinJoinHistoryItemViewModel(UiContext, _wallet, transaction),
			TransactionType.CoinjoinGroup => new CoinJoinsHistoryItemViewModel(UiContext, _wallet, transaction),
			TransactionType.Cancellation => new TransactionHistoryItemViewModel(UiContext, _wallet, transaction),
			TransactionType.CPFP => new SpeedUpHistoryItemViewModel(UiContext, _wallet, transaction, parent),
			_ => new TransactionHistoryItemViewModel(UiContext, _wallet, transaction)
		};

		var children = transaction.Children.Reverse();

		foreach (var child in children)
		{
			var historyItemViewModelBase = CreateViewModel(child, viewModel);
			viewModel.Children.Add(historyItemViewModelBase);
		}

		return viewModel;
	}
}
