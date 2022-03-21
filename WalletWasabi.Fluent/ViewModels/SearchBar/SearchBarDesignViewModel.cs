using System.Collections.ObjectModel;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchBarDesignViewModel : ReactiveObject
{
	public ReadOnlyObservableCollection<SearchItem> Items { get; } = new(new ObservableCollection<SearchItem>(new[]
	{
		new SearchItem("Test 1: Short", "Description short", null) {Icon = "settings_bitcoin_regular"},
		new SearchItem("Test 2: Loooooooooooong", "Description long", null) {Icon = "settings_bitcoin_regular"},
		new SearchItem("Test 3: Short again", "Description very very loooooooooooong and difficult to read", null)
			{Icon = "settings_bitcoin_regular"}, new SearchItem("Test 3", "Another", null) {Icon = "settings_bitcoin_regular"}
	}));

	public string SearchText => "Sample text";
}