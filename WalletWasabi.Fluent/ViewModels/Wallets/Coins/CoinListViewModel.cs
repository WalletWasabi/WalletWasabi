using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coins;

public class CoinListViewModel : ViewModelBase, ISortable, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<CoinListItem> _itemsCollection;
	private readonly IWalletModel _wallet;
	private IReadOnlyCollection<ICoinModel> _selectedCoins = ImmutableList<ICoinModel>.Empty;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	public CoinListViewModel(IWalletModel wallet, IList<ICoinModel> initialCoinSelection)
	{
		_wallet = wallet;

		var sourceItems = new SourceList<CoinListItem>().DisposeWith(_disposables);

		var changes = sourceItems.Connect();

		var coinItems = changes
			.TransformMany(
				item =>
				{
					// When root item is a coin item
					if (item is CoinViewModel c)
					{
						return new[] { c };
					}

					return item.Children;
				})
			.AddKey(model => model.Coin.Key);

		changes
			.Sort(SortExpressionComparer<CoinListItem>.Descending(x => x.AnonymityScore ?? x.Children.Min(c => c.AnonymityScore) ?? 0))
			.DisposeMany()
			.Bind(out _itemsCollection)
			.Subscribe()
			.DisposeWith(_disposables);

		coinItems
			.Bind(out var coinItemsCollection)
			.Subscribe()
			.DisposeWith(_disposables);

		var selectedCoins = coinItems
			.AutoRefresh(x => x.IsSelected)
			.ToCollection()
			.Select(GetSelectedCoins);

		wallet.Coins.Pockets
			.Connect()
			.ToCollection()
			.WithLatestFrom(selectedCoins, (pockets, sc) => (pockets, sc))
			.Do(
				tuple =>
				{
					var (pockets, sl) = tuple;
					var oldExpandedItemsLabel = _itemsCollection.Where(x => x.IsExpanded).Select(x => x.Labels).ToArray();
					RefreshFromPockets(sourceItems, pockets);
					UpdateSelection(coinItemsCollection, sl.ToList());
					RestoreExpandedRows(oldExpandedItemsLabel);
				})
			.Subscribe()
			.DisposeWith(_disposables);

		coinItems.AutoRefresh(x => x.IsSelected)
			.Filter(x => x.IsSelected == true)
			.Transform(x => x.Coin)
			.Bind(out var selection)
			.Subscribe();

		Selection = selection;

		TreeDataGridSource = CoinListDataGridSource.Create(_itemsCollection);
		TreeDataGridSource.DisposeWith(_disposables);

		wallet.Coins.Pockets
			.Connect()
			.ToCollection()
			.SkipWhile(pockets => pockets.Count == 0)
			.Do(
				pockets =>
				{
					RefreshFromPockets(sourceItems, pockets);
					UpdateSelection(coinItemsCollection, initialCoinSelection);
					ExpandSelectedItems();
				})
			.Subscribe();
		_wallet = wallet;

		ExpandAllCommand = ReactiveCommand.Create(
			() =>
			{
				foreach (var item in _itemsCollection)
				{
					item.IsExpanded = true;
				}
			});
	}

	public ReactiveCommand<Unit, Unit> ExpandAllCommand { get; set; }

	public ReadOnlyObservableCollection<ICoinModel> Selection { get; }

	public HierarchicalTreeDataGridSource<CoinListItem> TreeDataGridSource { get; }

	public ICommand StatusDescending => ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[0], ListSortDirection.Descending));
	public ICommand StatusAscending => ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[0], ListSortDirection.Ascending));
	public ICommand DateDescending => ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[1], ListSortDirection.Descending));
	public ICommand DateAscending => ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[1], ListSortDirection.Ascending));
	public ICommand AmountDescending => ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[2], ListSortDirection.Descending));
	public ICommand AmountAscending => ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[2], ListSortDirection.Ascending));
	public ICommand LabelDescending => ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[3], ListSortDirection.Descending));
	public ICommand LabelAscending => ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[3], ListSortDirection.Ascending));

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private static ReadOnlyCollection<ICoinModel> GetSelectedCoins(IReadOnlyCollection<CoinViewModel> list)
	{
		return new ReadOnlyCollection<ICoinModel>(list.Where(item => item.IsSelected == true).Select(x => x.Coin).ToList());
	}

	private static void UpdateSelection(IEnumerable<CoinViewModel> coinItems, IList<ICoinModel> selectedCoins)
	{
		var selectedSmartCoins = selectedCoins.GetSmartCoins().ToList();

		var coinsToSelect = coinItems.Where(x => selectedSmartCoins.Contains(x.Coin.GetSmartCoin()));

		foreach (var coinItem in coinsToSelect)
		{
			coinItem.IsSelected = true;
		}
	}

	private void RefreshFromPockets(ISourceList<CoinListItem> source, IEnumerable<Pocket> pockets)
	{
		var newItems =
			pockets.Select(pocket =>
			{
				// When it's single coin pocket, return its unique coin
				if (pocket.Coins.Count() == 1)
				{
					var coin = pocket.Coins.First();
					var coinModel = _wallet.Coins.GetCoinModel(coin);

					return (CoinListItem)new CoinViewModel(pocket.Labels, coinModel);
				}

				return new PocketViewModel(_wallet, pocket);
			});

		source.Edit(
			x =>
			{
				x.Clear();
				x.AddRange(newItems);
			});
	}

	private void RestoreExpandedRows(IEnumerable<LabelsArray> oldItemsLabels)
	{
		var itemsToExpand = _itemsCollection.Where(item => oldItemsLabels.Any(label => item.Labels.Equals(label)));

		foreach (var item in itemsToExpand)
		{
			item.IsExpanded = true;
		}
	}

	private void ExpandSelectedItems()
	{
		foreach (var item in _itemsCollection.Where(x => x.IsSelected is not false))
		{
			item.IsExpanded = true;
		}
	}
}
