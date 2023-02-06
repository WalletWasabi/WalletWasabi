using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.Views.CoinControl.Core.Cells;
using WalletWasabi.Fluent.Views.CoinControl.Core.Headers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.CoinControl;

public partial class CoinSelectorViewModel2 : ViewModelBase, IDisposable
{
	private readonly Wallet _wallet;
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private IReadOnlyCollection<SmartCoin> _selectedCoins = ImmutableList<SmartCoin>.Empty;
	private readonly ObservableCollection<CoinControlItemViewModelBase> _source;

	public CoinSelectorViewModel2(WalletViewModel walletViewModel, IList<SmartCoin> initialCoinSelection)
	{
		_wallet = walletViewModel.Wallet;
		_source = new ObservableCollection<CoinControlItemViewModelBase>();
		Refresh(_source, initialCoinSelection);

		walletViewModel.UiTriggers.TransactionsUpdateTrigger
			.Do(_ => Refresh())
			.Subscribe()
			.DisposeWith(_disposables);

		TreeDataGridSource = new HierarchicalTreeDataGridSource<CoinControlItemViewModelBase>(_source)
		{
			Columns =
			{
				ChildrenColumn(),
				IndicatorsColumn(),
				AmountColumn(),
				AnonymityScoreColumn(),
				PocketColumn()
			}
		};

		TreeDataGridSource.DisposeWith(_disposables);
	}

	public HierarchicalTreeDataGridSource<CoinControlItemViewModelBase> TreeDataGridSource { get; }

	public void Dispose()
	{
		_disposables.Dispose();
		Dispose(_source);
	}

	private IList<SmartCoin> GetSelectedCoins()
	{
		return _source
			.SelectMany(model => model.Children)
			.Cast<CoinCoinControlItemViewModel>()
			.Where(x => x.IsSelected == true)
			.Select(x => x.SmartCoin)
			.ToList();
	}

	private void SyncSelectedItems(IList<SmartCoin> selectedCoins)
	{
		var coinItems = _source.SelectMany(x => x.Children).Cast<CoinCoinControlItemViewModel>();
		foreach (var coinItem in coinItems)
		{
			coinItem.IsSelected = selectedCoins.Any(x => x == coinItem.SmartCoin);
		}
	}

	private static int GetLabelPriority(CoinControlItemViewModelBase coin)
	{
		if (coin.Labels == CoinPocketHelper.PrivateFundsText)
		{
			return 3;
		}

		if (coin.Labels == CoinPocketHelper.SemiPrivateFundsText)
		{
			return 2;
		}

		return 1;
	}

	private static int GetIndicatorPriority(CoinControlItemViewModelBase x)
	{
		if (x.IsCoinjoining)
		{
			return 1;
		}

		if (x.BannedUntilUtc.HasValue)
		{
			return 2;
		}

		if (!x.IsConfirmed)
		{
			return 3;
		}

		return 0;
	}

	private static Comparison<TSource?> SortAscending<TSource, TProperty>(Func<TSource, TProperty> selector)
	{
		return (x, y) => Comparer<TProperty>.Default.Compare(selector(x!), selector(y!));
	}

	private static Comparison<TSource?> SortDescending<TSource, TProperty>(Func<TSource, TProperty> selector)
	{
		return (x, y) => Comparer<TProperty>.Default.Compare(selector(y!), selector(x!));
	}

	private static IColumn<CoinControlItemViewModelBase> ChildrenColumn()
	{
		return new HierarchicalExpanderColumn<CoinControlItemViewModelBase>(
			SelectionColumn(),
			group => group.Children,
			node => node.Children.Count > 1,
			node => node.IsExpanded);
	}

	private static TemplateColumn<CoinControlItemViewModelBase> SelectionColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			"",
			new FuncDataTemplate<CoinControlItemViewModelBase>(
				(_, _) => new SelectionCellView(),
				true),
			GridLength.Auto);
	}

	private static IColumn<CoinControlItemViewModelBase> AmountColumn()
	{
		return new PlainTextColumn<CoinControlItemViewModelBase>(
			"Amount",
			node => node.Amount.ToFormattedString(),
			GridLength.Auto,
			new ColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = SortAscending<CoinControlItemViewModelBase, Money>(x => x.Amount),
				CompareDescending = SortDescending<CoinControlItemViewModelBase, Money>(x => x.Amount)
			});
	}

	private static IColumn<CoinControlItemViewModelBase> IndicatorsColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			"",
			new FuncDataTemplate<CoinControlItemViewModelBase>((_, _) => new IndicatorsCellView(), true),
			GridLength.Auto,
			new ColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = SortAscending<CoinControlItemViewModelBase, int>(GetIndicatorPriority),
				CompareDescending = SortDescending<CoinControlItemViewModelBase, int>(GetIndicatorPriority)
			});
	}

	private static IColumn<CoinControlItemViewModelBase> AnonymityScoreColumn()
	{
		return new PlainTextColumn<CoinControlItemViewModelBase>(
			new AnonymityScoreHeaderView(),
			node => node.AnonymityScore.ToString(),
			GridLength.Auto,
			new TextColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = SortAscending<CoinControlItemViewModelBase, int?>(b => b.AnonymityScore),
				CompareDescending = SortDescending<CoinControlItemViewModelBase, int?>(b => b.AnonymityScore)
			});
	}

	private static IColumn<CoinControlItemViewModelBase> PocketColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			"Pocket",
			new FuncDataTemplate<CoinControlItemViewModelBase>((_, _) => new LabelsCellView(), true),
			GridLength.Star,
			new ColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = SortAscending<CoinControlItemViewModelBase, int>(GetLabelPriority),
				CompareDescending = SortDescending<CoinControlItemViewModelBase, int>(GetLabelPriority)
			});
	}

	private void Refresh()
	{
		Refresh(_source, GetSelectedCoins());
	}

	private void Refresh(IList<CoinControlItemViewModelBase> collection, IList<SmartCoin> selectedItems)
	{
		Populate(collection, _wallet);
		SyncSelectedItems(selectedItems);
	}

	private static void Populate(IList<CoinControlItemViewModelBase> pocketItems, Wallet wallet)
	{
		var newItems = wallet
			.GetPockets()
			.Select(pocket => new PocketCoinControlItemViewModel(pocket));

		Dispose(pocketItems);

		pocketItems.Clear();
		pocketItems.AddRange(newItems);
	}

	private static void Dispose(IEnumerable<CoinControlItemViewModelBase> items)
	{
		foreach (var pocket in items.OfType<PocketCoinControlItemViewModel>())
		{
			pocket.Dispose();
		}
	}
}
