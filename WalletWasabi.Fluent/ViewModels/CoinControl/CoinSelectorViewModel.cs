using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.CoinControl;

public partial class CoinSelectorViewModel : ViewModelBase, IDisposable
{
	private readonly ReadOnlyObservableCollection<CoinControlItemViewModelBase> _itemsCollection;
	private readonly Wallet _wallet;
	private readonly CompositeDisposable _disposables = new();

	[AutoNotify] private IReadOnlyCollection<SmartCoin> _selectedCoins = ImmutableList<SmartCoin>.Empty;

	public CoinSelectorViewModel(WalletViewModel walletViewModel, IList<SmartCoin> initialCoinSelection)
	{
		_wallet = walletViewModel.Wallet;
		var sourceItems = new SourceList<CoinControlItemViewModelBase>();
		sourceItems.DisposeWith(_disposables);

		var coinItems = sourceItems
			.Connect()
			.TransformMany(x => x.Children)
			.Cast(x => (CoinCoinControlItemViewModel) x);

		sourceItems
			.Connect()
			.DisposeMany()
			.Bind(out _itemsCollection)
			.Subscribe()
			.DisposeWith(_disposables);

		coinItems
			.Bind(out var coinItemsCollection)
			.Subscribe()
			.DisposeWith(_disposables);

		walletViewModel.UiTriggers.TransactionsUpdateTrigger
			.Do(_ =>
			{
				var previousSelection = SelectedCoins.ToList();
				RefreshFromPockets(sourceItems);
				UpdateSelection(coinItemsCollection, previousSelection);
			})
			.Subscribe()
			.DisposeWith(_disposables);

		coinItems
			.AutoRefresh(x => x.IsSelected, TimeSpan.FromSeconds(0.1), scheduler: RxApp.MainThreadScheduler)
			.ToCollection()
			.Select(GetSelectedCoins)
			.BindTo(this, x => x.SelectedCoins)
			.DisposeWith(_disposables);

		TreeDataGridSource = CoinSelectorDataGridSource.Create(_itemsCollection);

		TreeDataGridSource.DisposeWith(_disposables);

		RefreshFromPockets(sourceItems);
		UpdateSelection(coinItemsCollection, initialCoinSelection);
		CollapseUnselectedPockets();
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

	private void RefreshFromPockets(ISourceList<CoinControlItemViewModelBase> source)
	{
		var newItems = _wallet
			.GetPockets()
			.Select(pocket => new PocketCoinControlItemViewModel(pocket));

		source.Edit(
			x =>
			{
				x.Clear();
				x.AddRange(newItems);
			});
	}

	private void CollapseUnselectedPockets()
	{
		foreach (var pocket in _itemsCollection.Where(x => x.IsSelected == false))
		{
			pocket.IsExpanded = false;
		}
	}

	private void UpdateSelection(IEnumerable<CoinCoinControlItemViewModel> coinItems, IList<SmartCoin> selectedCoins)
	{
		foreach (var coinItem in coinItems)
		{
			coinItem.IsSelected = selectedCoins.Any(x => x == coinItem.SmartCoin);
		}
	}
}
