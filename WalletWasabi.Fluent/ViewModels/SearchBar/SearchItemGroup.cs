using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchItemGroup
{
	private readonly ReadOnlyObservableCollection<ISearchItem> _items;

	public SearchItemGroup(string title, IObservableCache<ISearchItem, ComposedKey> groupCache)
	{
		Title = title;
		groupCache.Connect()
			.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe();
	}

	public string Title { get; }

	public ReadOnlyObservableCollection<ISearchItem> Items => _items;
}