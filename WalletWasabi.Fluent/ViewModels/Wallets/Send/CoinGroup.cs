using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class CoinGroup : IDisposable
{
	private readonly ReadOnlyObservableCollection<WalletCoinViewModel> _items;
	private readonly CompositeDisposable _disposables = new();
	public SmartLabel Labels { get; }

	public CoinGroup(SmartLabel labels, IObservableCache<WalletCoinViewModel, int> coins)
	{
		Labels = labels;

		coins.Connect()
			.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public ReadOnlyObservableCollection<WalletCoinViewModel> Items => _items;

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
