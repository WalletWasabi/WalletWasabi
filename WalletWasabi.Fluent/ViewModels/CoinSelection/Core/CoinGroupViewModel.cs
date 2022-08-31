using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public partial class CoinGroupViewModel : IDisposable, IThreeStateSelectable
{
	private readonly ReadOnlyObservableCollection<WalletCoinViewModel> _items;
	private readonly CompositeDisposable _disposables = new();
	public SmartLabel Labels { get; }
	[AutoNotify] private TreeStateSelection _treeStateSelection;

	private bool _canUpdate = true;

	public CoinGroupViewModel(SmartLabel labels, IObservable<IChangeSet<WalletCoinViewModel, int>> coins)
	{
		Labels = labels;

		coins.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe()
			.DisposeWith(_disposables);

		TotalAmount = coins
			.ToCollection()
			.Select(coinViewModels => new Money(coinViewModels.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC)), MoneyUnit.BTC));

		coins
			.AutoRefresh(x => x.IsSelected)
			.ToCollection()
			.Select(GetState)
			.Do(_ => _canUpdate = false)
			.Do(b => TreeStateSelection = b)
			.Do(_ => _canUpdate = true)
			.Subscribe()
			.DisposeWith(_disposables);

		this.WhenAnyValue<CoinGroupViewModel, TreeStateSelection>(x => x.TreeStateSelection)
			.Where(_ => _canUpdate)
			.Do(isSelected => Items.ToList().ForEach(vm => vm.IsSelected = isSelected == TreeStateSelection.True))
			.Subscribe()
			.DisposeWith(_disposables);
	}

	private static TreeStateSelection GetState(IReadOnlyCollection<WalletCoinViewModel> walletCoinViewModels)
	{
		var all = walletCoinViewModels.All(model => model.IsSelected);
		if (all)
		{
			return TreeStateSelection.True;
		}

		if (walletCoinViewModels.Any(x => x.IsSelected))
		{
			return TreeStateSelection.Partial;
		}

		return TreeStateSelection.False;
	}

	public IObservable<Money> TotalAmount { get; }

	public IEnumerable<WalletCoinViewModel> Items => _items;

	public void Dispose()
	{
		TreeStateSelection = TreeStateSelection.False;
		_disposables.Dispose();
	}
}
