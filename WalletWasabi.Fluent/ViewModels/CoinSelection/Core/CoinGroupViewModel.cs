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

public class CoinGroupViewModel : ReactiveObject, ICoin, IDisposable
{
	private readonly ObservableAsPropertyHelper<Money> _amount;
	private readonly ObservableAsPropertyHelper<int> _anonymitySet;
	private readonly ObservableAsPropertyHelper<DateTimeOffset?> _bannedUntil;
	private readonly CompositeDisposable _disposables = new();
	private readonly ObservableAsPropertyHelper<bool> _isCoinjoining;
	private readonly ObservableAsPropertyHelper<bool> _isConfirmed;
	private readonly ReadOnlyObservableCollection<ISelectable> _items;
	private readonly ObservableAsPropertyHelper<OutPoint> _outPoint;
	private readonly ObservableAsPropertyHelper<PrivacyLevel> _privacyLevel;
	private readonly ObservableAsPropertyHelper<SmartLabel> _smartLabel;

	public CoinGroupViewModel(PrivacyIndex privacyIndex, IObservable<IChangeSet<SelectableCoin, OutPoint>> coinChanges)
	{
		PrivacyIndex = privacyIndex;
		Labels = privacyIndex.Labels;

		coinChanges
			.Cast(x => (ISelectable) x)
			.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe()
			.DisposeWith(_disposables);

		_amount = coinChanges
			.ToCollection()
			.Select(coins => new Money(coins.Sum(coin => coin.Amount)))
			.ToProperty(this, x => x.Amount);

		_anonymitySet = coinChanges
			.ToCollection()
			.Select(coins => coins.Min(x => x.AnonymitySet))
			.ToProperty(this, x => x.AnonymitySet);

		_outPoint = coinChanges
			.ToCollection()
			.Select(
				coins =>
				{
					// IMPORTANT: We use the OutPoint.Zero value as a special value.
					// OutPoint.Zero means that "this coin has non-displayable data". The group sets this value when it doesn't act like a coin (I. e. has a single coin).
					return coins.Count == 1 ? coins.First().OutPoint : OutPoint.Zero;
				})
			.ToProperty(this, x => x.OutPoint);

		_isConfirmed = coinChanges
			.ToCollection()
			.Select(coins => coins.Any(x => x.IsConfirmed))
			.ToProperty(this, x => x.IsConfirmed);

		_isCoinjoining = coinChanges
			.ToCollection()
			.Select(coins => coins.Any(x => x.IsCoinjoining))
			.ToProperty(this, x => x.IsCoinjoining);

		_smartLabel = coinChanges
			.ToCollection()
			.Select(coins => coins.Select(coin => coin.SmartLabel).FirstOrDefault() ?? SmartLabel.Empty)
			.ToProperty(this, x => x.SmartLabel);

		_privacyLevel = coinChanges
			.ToCollection()
			.Select(coins => coins.Select(coin => coin.PrivacyLevel).First())
			.ToProperty(this, x => x.PrivacyLevel);

		_bannedUntil = coinChanges
			.ToCollection()
			.Select(coins => coins.Select(coin => coin.BannedUntil).FirstOrDefault())
			.ToProperty(this, x => x.BannedUntil);
	}

	public SmartLabel Labels { get; }
	public ReadOnlyObservableCollection<ISelectable> Items => _items;
	public PrivacyIndex PrivacyIndex { get; }
	public DateTimeOffset? BannedUntil => _bannedUntil.Value;
	public Money Amount => _amount.Value;
	public int AnonymitySet => _anonymitySet.Value;
	public SmartLabel SmartLabel => _smartLabel.Value;
	public PrivacyLevel PrivacyLevel => _privacyLevel.Value;
	public bool IsConfirmed => _isConfirmed.Value;
	public bool IsCoinjoining => _isCoinjoining.Value;
	public OutPoint OutPoint => _outPoint.Value;

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
