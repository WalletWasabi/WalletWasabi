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

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Model;

public partial class CoinGroupViewModel : IDisposable, IThreeState
{
	private readonly ReadOnlyObservableCollection<WalletCoinViewModel> _items;
	private readonly CompositeDisposable _disposables = new();
	public SmartLabel Labels { get; }
	[AutoNotify] private SelectionState _selectionState;

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
			.Do(b => SelectionState = b)
			.Do(_ => _canUpdate = true)
			.Subscribe()
			.DisposeWith(_disposables);

		this.WhenAnyValue<CoinGroupViewModel, SelectionState>(x => x.SelectionState)
			.Where(_ => _canUpdate)
			.Do(isSelected => Items.ToList().ForEach(vm => vm.IsSelected = isSelected == SelectionState.True))
			.Subscribe()
			.DisposeWith(_disposables);
	}

	private static SelectionState GetState(IReadOnlyCollection<WalletCoinViewModel> walletCoinViewModels)
	{
		var all = walletCoinViewModels.All(model => model.IsSelected);
		if (all)
		{
			return SelectionState.True;
		}

		if (walletCoinViewModels.Any(x => x.IsSelected))
		{
			return SelectionState.Partial;
		}

		return SelectionState.False;
	}

	public IObservable<Money> TotalAmount { get; }

	public IEnumerable<WalletCoinViewModel> Items => _items;

	public void Dispose()
	{
		SelectionState = SelectionState.False;
		_disposables.Dispose();
	}
}
