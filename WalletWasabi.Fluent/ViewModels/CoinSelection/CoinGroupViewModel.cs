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

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public partial class CoinGroupViewModel : IDisposable, ISelectable
{
	private readonly ReadOnlyObservableCollection<WalletCoinViewModel> _items;
	private readonly CompositeDisposable _disposables = new();
	public SmartLabel Labels { get; }

	[AutoNotify] private bool _isSelected;
	private bool _canUpdate = true;

	public CoinGroupViewModel(SmartLabel labels, IConnectableCache<WalletCoinViewModel, int> coins)
	{
		Labels = labels;

		coins.Connect()
			.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe()
			.DisposeWith(_disposables);

		TotalAmount = coins
			.Connect()
			.ToCollection()
			.Select(coinViewModels => new Money(coinViewModels.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC)), MoneyUnit.BTC));

		coins
			.Connect()
			.AutoRefresh(x => x.IsSelected)
			.ToCollection()
			.Select(x => x.All(x => x.IsSelected))
			.Do(_ => _canUpdate = false)
			.Do(b => IsSelected = b)
			.Do(_ => _canUpdate = true)
			.Subscribe()
			.DisposeWith(_disposables);

		this.WhenAnyValue(x => x.IsSelected)
			.Where(b => _canUpdate)
			.Do(isSelected => Items.ToList().ForEach(vm => vm.IsSelected = isSelected))
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public IObservable<Money> TotalAmount { get; }

	public IEnumerable<WalletCoinViewModel> Items => _items;

	public void Dispose()
	{
		IsSelected = false;
		_disposables.Dispose();
	}
}
