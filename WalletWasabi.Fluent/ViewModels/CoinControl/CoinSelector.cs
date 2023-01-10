using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.Views.CoinControl.Core.Cells;
using WalletWasabi.Fluent.Views.CoinControl.Core.Headers;

namespace WalletWasabi.Fluent.ViewModels.CoinControl;

public class CoinSelector : ViewModelBase
{
	private readonly BehaviorSubject<IEnumerable<SmartCoin>> _selectedCoinsSubject;

	public CoinSelector(WalletViewModel walletViewModel, IEnumerable<SmartCoin> selectedCoins)
	{
		var pockets = walletViewModel.Wallet.GetPockets();
		var pocketItems = CreatePocketItems(pockets);
		
		SyncSelectedItems(pocketItems, selectedCoins);

		var pocketColumn = PocketColumn();
		Source = new HierarchicalTreeDataGridSource<CoinControlItemViewModelBase>(pocketItems)
		{
			Columns =
			{
				ChildrenColumn(),
				IndicatorsColumn(),
				AmountColumn(),
				AnonymityScoreColumn(),
				pocketColumn
			}
		};

		var coins = pocketItems
			.SelectMany(x => x.Children)
			.OfType<CoinCoinControlItemViewModel>()
			.ToList();

		_selectedCoinsSubject = new BehaviorSubject<IEnumerable<SmartCoin>>(new List<SmartCoin>());

		SelectedCoins = coins
			.AsObservableChangeSet(x => x.SmartCoin)
			.AutoRefresh(x => x.IsSelected)
			.ToCollection()
			.Select(x => x.Where(m => m.IsSelected == true))
			.Select(models => models.Select(x => x.SmartCoin));

		SelectedCoins.Subscribe(_selectedCoinsSubject);
	}

	public IEnumerable<SmartCoin> SelectedCoinsValue => _selectedCoinsSubject.Value;

	public IObservable<IEnumerable<SmartCoin>> SelectedCoins { get; }

	public HierarchicalTreeDataGridSource<CoinControlItemViewModelBase> Source { get; }

	private static IList<CoinCoinControlItemViewModel> GetAllCoinItems(IEnumerable<PocketCoinControlItemViewModel> pockets)
	{
		return pockets
			.SelectMany(x => x.Children)
			.OfType<CoinCoinControlItemViewModel>()
			.ToList();
	}

	private static IList<SmartCoin> GetSelectedCoinItems(IEnumerable<PocketCoinControlItemViewModel> pocketItems)
	{
		return GetAllCoinItems(pocketItems)
			.Where(x => x.IsSelected == true)
			.Select(x => x.SmartCoin)
			.ToList();
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

	private static void SyncSelectedItems(IEnumerable<PocketCoinControlItemViewModel> items, IEnumerable<SmartCoin> selectedCoins)
	{
		var allCoins = GetAllCoinItems(items);
		var selected = allCoins.Where(x => selectedCoins.Any(other => other == x.SmartCoin));
		foreach (var viewModel in selected)
		{
			viewModel.IsSelected = true;
		}
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
			node => node is CoinCoinControlItemViewModel coin ? coin.AnonymityScore.ToString() : "",
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

	private static IReadOnlyCollection<PocketCoinControlItemViewModel> CreatePocketItems(IEnumerable<Pocket> pockets)
	{
		return pockets
			.Select(pocket => new PocketCoinControlItemViewModel(pocket))
			.ToList();
	}

	private static Comparison<TSource> SortAscending<TSource, TProperty>(Func<TSource, TProperty> selector)
	{
		return (x, y) => Comparer<TProperty>.Default.Compare(selector(x!), selector(y!));
	}

	private static Comparison<TSource> SortDescending<TSource, TProperty>(Func<TSource, TProperty> selector)
	{
		return (x, y) => Comparer<TProperty>.Default.Compare(selector(y!), selector(x!));
	}
}
