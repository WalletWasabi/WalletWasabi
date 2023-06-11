using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public partial class SearchBarViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<SearchItemGroup> _groups;
	[AutoNotify] private bool _isSearchListVisible;
	[AutoNotify] private string _searchText = "";

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
			.ReplayLastActive();

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

	public ICommand ResetCommand { get; }

	public ICommand ActivateFirstItemCommand { get; set; }

	public IObservable<bool> HasResults { get; }

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;

	private void ClearAndHideSearchList()
	{
		IsSearchListVisible = false;
		SearchText = "";
	}
}
