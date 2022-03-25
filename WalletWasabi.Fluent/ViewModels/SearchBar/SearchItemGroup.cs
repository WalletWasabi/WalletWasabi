using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchItemGroup
{
	private readonly ReadOnlyObservableCollection<SearchItemViewModel> _items;

	public SearchItemGroup(string title, IObservableCache<SearchItemViewModel, ComposedKey> groupCache)
	{
		Title = title;
		groupCache.Connect()
			.Bind(out _items)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe();
	}

	public string Title { get; }

	public ReadOnlyObservableCollection<SearchItemViewModel> Items => _items;
}