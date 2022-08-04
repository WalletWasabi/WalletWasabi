using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Aggregation;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public partial class SearchBarViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<SearchItemGroup> _groups;
	private readonly ObservableAsPropertyHelper<bool> _hasResults;
	[AutoNotify] private bool _isSearchListVisible;
	[AutoNotify] private string _searchText = "";

	public SearchBarViewModel(IObservable<IChangeSet<ISearchItem, ComposedKey>> itemsObservable)
	{
		var filterPredicate = this
			.WhenAnyValue(x => x.SearchText)
			.Throttle(TimeSpan.FromMilliseconds(250), RxApp.MainThreadScheduler)
			.DistinctUntilChanged()
			.Select(SearchItemFilterFunc);

		var filteredItems = itemsObservable
			.RefCount()
			.Filter(filterPredicate);

		filteredItems
			.Transform(item => item is ActionableItem i ? new AutocloseActionableItem(i, () => IsSearchListVisible = false) : item)
			.Group(s => s.Category)
			.Transform(group => new SearchItemGroup(group.Key, group.Cache))
			.Bind(out _groups)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe();

		_hasResults = filteredItems
			.Count()
			.Select(i => i > 0)
			.ToProperty(this, x => x.HasResults);
	}

	public bool HasResults => _hasResults.Value;

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;

	private static Func<ISearchItem, bool> SearchItemFilterFunc(string? text)
	{
		return searchItem =>
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return searchItem.IsDefault;
			}

			var containsName = searchItem.Name.Contains(text, StringComparison.InvariantCultureIgnoreCase);
			var containsCategory = searchItem.Category.Contains(text, StringComparison.InvariantCultureIgnoreCase);
			var containsAnyTag =
				searchItem.Keywords.Any(s => s.Contains(text, StringComparison.InvariantCultureIgnoreCase));
			return containsName || containsCategory || containsAnyTag;
		};
	}
}
