using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class CoinGroup : ReactiveObject, IDisposable
{
	private readonly ReadOnlyObservableCollection<WalletCoinViewModel> _items;
	private readonly CompositeDisposable _disposables = new();
	public SmartLabel Labels { get; }

	[AutoNotify] private bool _isSelected;

	public CoinGroup(SmartLabel labels, IObservableCache<WalletCoinViewModel, int> coins)
	{
		Labels = labels;

		coins.Connect()
			.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CoinListUtils.CreateGridSource(_items);
		TotalAmount = coins
			.Connect()
			.ToCollection()
			.Select(coinViewModels => new Money(coinViewModels.Sum(walletCoinViewModel => walletCoinViewModel.Amount.ToDecimal(MoneyUnit.BTC)), MoneyUnit.BTC));

		this.WhenAnyValue(x => x.IsSelected)
			.Do(isSelected => Source.Items.ToList().ForEach(vm => vm.IsSelected = isSelected))
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public IObservable<Money> TotalAmount { get; }

	public FlatTreeDataGridSource<WalletCoinViewModel> Source { get; }

	public ReadOnlyObservableCollection<WalletCoinViewModel> Items => _items;
	
	public void Dispose()
	{
		IsSelected = false;
		Source.Dispose();
		_disposables.Dispose();
	}
}
