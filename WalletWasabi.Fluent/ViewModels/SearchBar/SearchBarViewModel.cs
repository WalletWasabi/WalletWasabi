using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public partial class SearchBarViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<SearchItemGroup> _groups;
	[AutoNotify] private bool _isSearchListVisible;
	[AutoNotify] private string _searchText = "";
	private readonly ObservableAsPropertyHelper<bool> _hasResults;

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

		_hasResults = itemsObservable
			.ToCollection()
			.Select(x => x.Any())
			.ToProperty(this, x => x.HasResults);

		ActivateFirstItemCommand = ReactiveCommand.Create(
			() =>
			{
				if (_groups is [{ Items: [IActionableItem item] }])
				{
					item.Activate();
					ClearAndHideSearchList();
				}
			});

		ResetCommand = ReactiveCommand.Create(ClearAndHideSearchList);
	}

	public bool HasResults => _hasResults.Value;

	public ICommand ResetCommand { get; }

	public ICommand ActivateFirstItemCommand { get; set; }

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;

	private void ClearAndHideSearchList()
	{
		IsSearchListVisible = false;
		SearchText = "";
	}
}
