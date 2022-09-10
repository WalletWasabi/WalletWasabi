using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Headers;

public partial class SelectionHeaderViewModelBase<TKey> : ViewModelBase where TKey : notnull
{
	[AutoNotify] private bool _isSelected;

	public SelectionHeaderViewModelBase(IObservable<IChangeSet<ISelectable, TKey>> changeStream, Func<int, string> getContent)
	{
		var collectionChanged = changeStream
			.AutoRefresh(x => x.IsSelected)
			.ToCollection();

		Text = collectionChanged
			.Select(x => getContent(x.Count(y => y.IsSelected)))
			.ReplayLastActive();

		this.WhenAnyValue(x => x.IsSelected).Skip(1)
			.WithLatestFrom(collectionChanged)
			.Do(tuple => tuple.Second.ToList().ForEach(x => x.IsSelected = tuple.First))
			.Subscribe();
	}

	public IObservable<string> Text { get; }
}
