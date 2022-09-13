using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

public class CoinGroupViewModel : IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<ISelectable> _items;

	public CoinGroupViewModel(PrivacyLevelKey key, IObservable<IChangeSet<WalletCoinViewModel, OutPoint>> coins)
	{
		Key = key;
		Labels = key.Labels;

		if (Labels.IsEmpty)
		{
			PrivacyLevel = key.PrivacyLevel;
		}

		coins
			.Cast(x => (ISelectable) x)
			.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe()
			.DisposeWith(_disposables);

		TotalAmount = coins
			.ToCollection()
			.Select(
				coinViewModels => new Money(coinViewModels.Sum(x => x.Amount.ToDecimal(MoneyUnit.BTC)), MoneyUnit.BTC));
	}

	public SmartLabel Labels { get; }

	public IObservable<Money> TotalAmount { get; }

	public ReadOnlyObservableCollection<ISelectable> Items => _items;

	public PrivacyLevel? PrivacyLevel { get; }

	public PrivacyLevelKey Key { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
