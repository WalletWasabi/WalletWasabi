using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Controls;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public class CoinGroupViewModel : IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<ISelectable> _items;

	public CoinGroupViewModel(PrivacyIndex privacyIndex, IObservable<IChangeSet<SelectableCoin, OutPoint>> coins)
	{
		PrivacyIndex = privacyIndex;
		Labels = privacyIndex.Labels;

		coins
			.Cast(x => (ISelectable) x)
			.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe()
			.DisposeWith(_disposables);

		TotalAmount = coins
			.ToCollection()
			.Select(coinViewModels => new Money(coinViewModels.Sum(x => x.Coin.Amount.ToDecimal(MoneyUnit.BTC)), MoneyUnit.BTC));
	}

	public SmartLabel Labels { get; }

	public IObservable<Money> TotalAmount { get; }

	public ReadOnlyObservableCollection<ISelectable> Items => _items;

	public PrivacyIndex PrivacyIndex { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
