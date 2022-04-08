using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchBarDesignViewModel : ReactiveObject
{
	private readonly IEnumerable<ISearchItem> _items;

	public SearchBarDesignViewModel()
	{
		var actionable = new IActionableItem[]
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
		}.Select(item => (ISearchItem) new AutocloseActionableItem(item, () => { }));

		var nonActionable = new ISearchItem[]
		{
			new NonActionableSearchItem(new DarkThemeSelector(), "Dark theme", "Appearance", new List<string>(), null)
		};

		_items = actionable.Concat(nonActionable).ToList();
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