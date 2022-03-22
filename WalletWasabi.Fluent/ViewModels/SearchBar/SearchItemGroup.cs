using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBarTextPart;

public class SearchItemGroup
{
	private readonly ReadOnlyObservableCollection<SearchItem> _items;

	public SearchItemGroup(string title, IObservableCache<SearchItem, ComposedKey> groupCache)
	{
		Title = title;
		groupCache.Connect()
			.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe();
	}

	public string Title { get; }

	public ReadOnlyObservableCollection<SearchItem> Items => _items;
}