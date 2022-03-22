using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBarTextPart;

public class SearchBarViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<SearchItemGroup> _groups;
	private string _searchText;

	public SearchBarViewModel(IObservable<SearchItem> itemsObservable)
	{
		var source = new SourceCache<SearchItem, ComposedKey>(item => item.Key);
		source.PopulateFrom(itemsObservable);

		var filterPredicate = this
			.WhenAnyValue(x => x.SearchText)
			.Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
			.DistinctUntilChanged()
			.Select(SearchItemFilterFunc);

		source.Connect()
			.RefCount()
			.Filter(filterPredicate)
			.Group(s => s.Category)
			.Transform(group => new SearchItemGroup(group.Key, group.Cache))
			.Bind(out _groups)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe();
	}

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;

	public string SearchText
	{
		get => _searchText;
		set => this.RaiseAndSetIfChanged(ref _searchText, value);
	}

	private static Func<SearchItem, bool> SearchItemFilterFunc(string? text)
	{
		return searchItem =>
		{
			if (text is null)
			{
				return true;
			}

			var containsName = searchItem.Name.Contains(text, StringComparison.InvariantCultureIgnoreCase);
			var containsAnyTag = searchItem.Keywords.Any(s => s.Contains(text, StringComparison.InvariantCultureIgnoreCase));
			return containsName || containsAnyTag;
		};
	}
}