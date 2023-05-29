using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public partial class SearchBarViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<SearchItemGroup> _groups;
	[AutoNotify] private bool _isSearchListVisible;
	[AutoNotify] private string _searchText = "";

	public SearchBarViewModel(IObservable<IChangeSet<ISearchItem, ComposedKey>> itemsObservable, ISearchSource searchSource)
	{
		itemsObservable
			.Group(s => s.Category)
			.Transform(group => new SearchItemGroup(group.Key, group.Cache.Connect()))
			.Sort(SortExpressionComparer<SearchItemGroup>.Ascending(x => x.Title))
			.Bind(out _groups)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe();

		HasResults = itemsObservable
			.Count()
			.Select(i => i > 0)
			.Replay(1)
			.RefCount();

		NavigateToSearchCommand = ReactiveCommand.Create(() =>
		{
			searchSource.DoSearch(SearchText);
			IsSearchListVisible = false;
		});
	}

	public ICommand NavigateToSearchCommand { get; set; }

	public IObservable<bool> HasResults { get; }

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;
}
