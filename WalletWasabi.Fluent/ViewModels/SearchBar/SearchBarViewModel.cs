using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchBarViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<SearchItemGroup?> _groups;
	private readonly ObservableAsPropertyHelper<ICommand?> _selectedCommand;
	private bool _isSearchListVisible;
	private string _searchText;
	private SearchItem _selectedSearchItem;

	public SearchBarViewModel(IObservable<SearchItem> itemsObservable)
	{
		var vms = itemsObservable.Select(item => new SearchItemViewModel(item, () => { IsSearchListVisible = false; }));

		var source = new SourceCache<SearchItemViewModel, ComposedKey>(item => item.Key);
		source.PopulateFrom(vms);

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

		ShowListCommand = ReactiveCommand.Create(() => IsSearchListVisible = true);
	}

	public ReactiveCommand<Unit, bool> ShowListCommand { get; set; }

	public bool IsSearchListVisible
	{
		get => _isSearchListVisible;
		set => this.RaiseAndSetIfChanged(ref _isSearchListVisible, value);
	}

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;

	public string SearchText
	{
		get => _searchText;
		set => this.RaiseAndSetIfChanged(ref _searchText, value);
	}

	private static Func<SearchItemViewModel, bool> SearchItemFilterFunc(string? text)
	{
		return searchItem =>
		{
			if (text is null)
			{
				return true;
			}

			var containsName = searchItem.Name.Contains(text, StringComparison.InvariantCultureIgnoreCase);
			var containsAnyTag =
				searchItem.Keywords.Any(s => s.Contains(text, StringComparison.InvariantCultureIgnoreCase));
			return containsName || containsAnyTag;
		};
	}
}