using System.Collections.ObjectModel;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public partial class SearchBarViewModel : ObservableObject
{
	private readonly ReadOnlyObservableCollection<SearchItemGroup> _groups;
	[ObservableProperty] private bool _isSearchListVisible;
	[ObservableProperty] private string _searchText = "";

	public SearchBarViewModel(IObservable<IChangeSet<ISearchItem, ComposedKey>> itemsObservable)
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
	}

	public IObservable<bool> HasResults { get; }

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;
}
