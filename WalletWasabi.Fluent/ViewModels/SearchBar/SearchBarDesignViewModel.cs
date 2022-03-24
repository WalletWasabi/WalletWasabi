using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBarTextPart;

public class SearchBarDesignViewModel : ReactiveObject
{
	private readonly SearchItem[] _items;

	public SearchBarDesignViewModel()
	{
		_items = new[]
		{
			new SearchItem("Test 1: Short", "Description short", null, "Settings") {Icon = "settings_bitcoin_regular"},
			new SearchItem("Test 2: Loooooooooooong", "Description long", null, "Settings")
				{Icon = "settings_bitcoin_regular"},
			new SearchItem("Test 3: Short again", "Description very very loooooooooooong and difficult to read", null,
					"Settings")
				{Icon = "settings_bitcoin_regular"},
			new SearchItem("Test 3", "Another", null, "Settings") {Icon = "settings_bitcoin_regular"},
			new SearchItem("Test 4: Help topics", "Description very very loooooooooooong and difficult to read", null,
					"Help")
				{Icon = "settings_bitcoin_regular"},
			new SearchItem("Test 3", "Another", null, "Help") {Icon = "settings_bitcoin_regular"}
		};
	}

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => new(new ObservableCollection<SearchItemGroup>(_items
		.GroupBy(r => r.Category)
		.Select(g =>
		{
			var sourceCache = new SourceCache<SearchItem, ComposedKey>(r => r.Key);
			var observable = g.ToObservable();
			sourceCache.PopulateFrom(observable);
			return new SearchItemGroup(g.Key, sourceCache);
		})));

	public string SearchText => "";
}