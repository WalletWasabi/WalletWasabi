using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchBarDesignViewModel : ReactiveObject
{
	private readonly ActionableItem[] _items;

	public SearchBarDesignViewModel()
	{
		_items = new[]
		{
			new ActionableItem("Test 1: Short", "Description short", null, "Settings")
				{Icon = "settings_bitcoin_regular"},
			new ActionableItem("Test 2: Loooooooooooong", "Description long", null, "Settings")
				{Icon = "settings_bitcoin_regular"},
			new ActionableItem("Test 3: Short again", "Description very very loooooooooooong and difficult to read",
					null,
					"Settings")
				{Icon = "settings_bitcoin_regular"},
			new ActionableItem("Test 3", "Another", null, "Settings") {Icon = "settings_bitcoin_regular"},
			new ActionableItem("Test 4: Help topics", "Description very very loooooooooooong and difficult to read",
					null,
					"Help")
				{Icon = "settings_bitcoin_regular"},
			new ActionableItem("Test 3", "Another", null, "Help") {Icon = "settings_bitcoin_regular"}
		};
	}

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => new(new ObservableCollection<SearchItemGroup>(_items
		.GroupBy(r => r.Category)
		.Select(grouping =>
		{
			var sourceCache = new SourceCache<ISearchItem, ComposedKey>(r => r.Key);
			sourceCache.PopulateFrom(grouping.ToObservable());
			return new SearchItemGroup(grouping.Key, sourceCache);
		})));

	public string SearchText => "";
}