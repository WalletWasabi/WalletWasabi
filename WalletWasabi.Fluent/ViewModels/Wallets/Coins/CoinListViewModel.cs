using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Controls.Sorting;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coins;

public class CoinListViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<CoinListItem> _itemsCollection;
	private readonly bool _ignorePrivacyMode;
	private readonly bool _allowCoinjoiningCoinSelection;

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Uses DisposeWith()")]
	public CoinListViewModel(ICoinListModel availableCoins, IList<CoinModel> initialCoinSelection, bool allowCoinjoiningCoinSelection, bool ignorePrivacyMode, bool allowSelection = true)
	{
		_ignorePrivacyMode = ignorePrivacyMode;
		_allowCoinjoiningCoinSelection = allowCoinjoiningCoinSelection;

		var viewModels = new SourceList<CoinListItem>().DisposeWith(_disposables);

		var changes = viewModels.Connect();

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

		coinItems.OnItemAdded(model => model.Coin.SubscribeToCoinChanges(_disposables))
			.Subscribe()
			.DisposeWith(_disposables);

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

		coinItems.AutoRefresh(x => x.IsSelected)
			.Filter(x => x.IsSelected == true)
			.Transform(x => x.Coin)
			.Bind(out var selection)
			.Subscribe()
			.DisposeWith(_disposables);

		Selection = selection;

		availableCoins.Pockets
			.Connect(suppressEmptyChangeSets: false)
			.ToCollection()
			.Do(
				pockets =>
				{
					IList<CoinModel> oldSelection = Selection.ToArray();
					var oldExpandedItemsLabel = _itemsCollection.Where(x => x.IsExpanded).Select(x => x.Labels).ToArray();
					Rebuild(viewModels, pockets, availableCoins);
					UpdateSelection(coinItemsCollection, oldSelection);
					RestoreExpandedRows(oldExpandedItemsLabel);
				})
			.Subscribe()
			.DisposeWith(_disposables);

		TreeDataGridSource = CoinListDataGridSource.Create(_itemsCollection, allowSelection);
		TreeDataGridSource.DisposeWith(_disposables);
		CoinItems = coinItemsCollection;

		ExpandAllCommand = ReactiveCommand.Create(
			() =>
			{
				foreach (var item in _itemsCollection)
				{
					item.IsExpanded = true;
				}
			});

		Sortables =
		[
			new SortableItem("Status") { SortByAscendingCommand = ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[0], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[0], ListSortDirection.Descending)) },
			new SortableItem("Date") { SortByAscendingCommand = ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[1], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[1], ListSortDirection.Descending)) },
			new SortableItem("Amount") { SortByAscendingCommand = ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[2], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[2], ListSortDirection.Descending)) },
			new SortableItem("Label") { SortByAscendingCommand = ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[3], ListSortDirection.Ascending)), SortByDescendingCommand = ReactiveCommand.Create(() => TreeDataGridSource.SortBy(TreeDataGridSource.Columns[3], ListSortDirection.Descending)) },
		];

		SetInitialSelection(initialCoinSelection);
	}

	private void SetInitialSelection(IEnumerable<CoinModel> initialSelection)
	{
		var initialSmartCoins = initialSelection.GetSmartCoins().ToList();
		var coinsToSelect = CoinItems.Where(x => initialSmartCoins.Contains(x.Coin.GetSmartCoin()));

		foreach (var coinItem in coinsToSelect)
		{
			coinItem.IsSelected = true;
		}
	}

	public ReadOnlyObservableCollection<CoinViewModel> CoinItems { get; }

	public ReactiveCommand<Unit, Unit> ExpandAllCommand { get; set; }

	public ReadOnlyObservableCollection<CoinModel> Selection { get; }

	public HierarchicalTreeDataGridSource<CoinListItem> TreeDataGridSource { get; }

	public IEnumerable<SortableItem> Sortables { get; private set; }

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private static void UpdateSelection(IEnumerable<CoinViewModel> coinItems, IList<CoinModel> selectedCoins)
	{
		var selectedSmartCoins = selectedCoins.GetSmartCoins().ToList();

		var coinsToSelect = coinItems.Where(x => selectedSmartCoins.Contains(x.Coin.GetSmartCoin()));

		foreach (var coinItem in coinsToSelect)
		{
			coinItem.IsSelected = true;
		}
	}

	private void Rebuild(ISourceList<CoinListItem> source, IEnumerable<Pocket> pockets, ICoinListModel availableCoins)
	{
		var newItems =
			pockets.Select(pocket =>
			{
				// When it's single coin pocket, return its unique coin
				if (pocket.Coins.Count() == 1)
				{
					var coin = pocket.Coins.First();
					var coinModel = availableCoins.GetCoinModel(coin);

					return (CoinListItem)new CoinViewModel(pocket.Labels, coinModel, _ignorePrivacyMode, _allowCoinjoiningCoinSelection);
				}

				return new PocketViewModel(pocket, availableCoins, _allowCoinjoiningCoinSelection, _ignorePrivacyMode);
			});

		source.EditDiff(newItems, new LambdaComparer<CoinListItem>((a, b) => Equals(a?.Key, b?.Key)));
	}

	private void RestoreExpandedRows(IEnumerable<LabelsArray> oldItemsLabels)
	{
		var itemsToExpand = _itemsCollection.Where(item => oldItemsLabels.Any(label => item.Labels.Equals(label)));

		foreach (var item in itemsToExpand)
		{
			item.IsExpanded = true;
		}
	}
}
