using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class CoinSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	[AutoNotify]
	private FlatTreeDataGridSource<WalletCoinViewModel> _source = new(Enumerable.Empty<WalletCoinViewModel>());

	public CoinSelectionViewModel(IObservable<IChangeSet<WalletCoinViewModel, int>> coinChanges)
	{
		coinChanges
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var coinsCollection)
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CoinListUtils.CreateGridSource(coinsCollection)
			.DisposeWith(_disposables);
	}

	public void Dispose()
	{
		_source.Dispose();
		_disposables.Dispose();
	}
}
