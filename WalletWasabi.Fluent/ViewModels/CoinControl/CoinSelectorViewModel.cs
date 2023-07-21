using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.CoinControl;

public partial class CoinSelectorViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<CoinControlItemViewModelBase> _itemsCollection;
	private readonly Wallet _wallet;

	[AutoNotify] private IReadOnlyCollection<SmartCoin> _selectedCoins = ImmutableList<SmartCoin>.Empty;

	public CoinSelectorViewModel(WalletViewModel walletViewModel, IList<SmartCoin> initialCoinSelection)
	{
		_wallet = walletViewModel.Wallet;
		var sourceItems = new SourceList<CoinControlItemViewModelBase>();
		sourceItems.DisposeWith(_disposables);

		var changes = sourceItems.Connect();

		var coinItems = changes
			.TransformMany(item =>
			{
				// When root item is a coin item
				if (item is CoinCoinControlItemViewModel c)
				{
					return new[] { c };
				}

				return item.Children;
			})
			.AddKey(model => model.SmartCoin.Outpoint);

		changes
			.Sort(SortExpressionComparer<CoinControlItemViewModelBase>.Descending(x => x.AnonymityScore ?? x.Children.Min(c => c.AnonymityScore) ?? 0))
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

		walletViewModel.UiTriggers.TransactionsUpdateTrigger
			.WithLatestFrom(selectedCoins, (_, sc) => sc)
			.Do(
				sl =>
				{
					var oldExpandedItemsLabel = _itemsCollection.Where(x => x.IsExpanded).Select(x => x.Labels).ToArray();
					RefreshFromPockets(sourceItems);
					UpdateSelection(coinItemsCollection, sl.ToList());
					RestoreExpandedRows(oldExpandedItemsLabel);
				})
			.Subscribe()
			.DisposeWith(_disposables);

		// Project selected coins to public property. Throttle for improved UI performance
		selectedCoins
			.Throttle(TimeSpan.FromSeconds(0.1), RxApp.MainThreadScheduler)
			.BindTo(this, x => x.SelectedCoins)
			.DisposeWith(_disposables);

		TreeDataGridSource = CoinSelectorDataGridSource.Create(_itemsCollection);
		TreeDataGridSource.DisposeWith(_disposables);

		RefreshFromPockets(sourceItems);
		UpdateSelection(coinItemsCollection, initialCoinSelection);
		ExpandSelectedItems();
	}

	public HierarchicalTreeDataGridSource<CoinControlItemViewModelBase> TreeDataGridSource { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private static ReadOnlyCollection<SmartCoin> GetSelectedCoins(IReadOnlyCollection<CoinCoinControlItemViewModel> list)
	{
		return new ReadOnlyCollection<SmartCoin>(list.Where(item => item.IsSelected == true).Select(x => x.SmartCoin).ToList());
	}

	private static void UpdateSelection(IEnumerable<CoinCoinControlItemViewModel> coinItems, IList<SmartCoin> selectedCoins)
	{
		var coinsToSelect = coinItems.Where(x => selectedCoins.Contains(x.SmartCoin));

		foreach (var coinItem in coinsToSelect)
		{
			coinItem.IsSelected = true;
		}
	}

	private void RefreshFromPockets(ISourceList<CoinControlItemViewModelBase> source)
	{
		var newItems = _wallet
			.GetPockets()
			.Select(pocket =>
			{
				// When it's single coin pocket, return its unique coin
				if (pocket.Coins.Count() == 1)
				{
					return (CoinControlItemViewModelBase)new CoinCoinControlItemViewModel(pocket);
				}

				return new PocketCoinControlItemViewModel(pocket);
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
