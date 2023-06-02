using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public partial class SearchBarViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly ReadOnlyObservableCollection<SearchItemGroup> _groups;
	[AutoNotify] private bool _isSearchListVisible;
	[AutoNotify] private string _searchText = "";

	public SearchBarViewModel(ISearchSource searchSource)
	{
		searchSource.Changes
			.Group(s => s.Category)
			.Transform(group => new SearchItemGroup(group.Key, group.Cache.Connect()))
			.Sort(SortExpressionComparer<SearchItemGroup>.Ascending(x => x.Title))
			.Bind(out _groups)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe()
			.DisposeWith(_disposables);

		HasResults = searchSource.Changes
			.Count()
			.Select(i => i > 0)
			.ReplayLastActive();

		searchSource.Changes
			.Bind(out var results)
			.Subscribe()
			.DisposeWith(_disposables);

		var activateFirstItemCommand = ReactiveCommand.CreateFromTask(() => ((IActionableItem) results.First()).Activate(), HasSingleItem(searchSource));

		ActivateFirstItemCommand = activateFirstItemCommand;
		
		activateFirstItemCommand
			.Do(_ => Reset())
			.Subscribe()
			.DisposeWith(_disposables);

		FirstItemActivated = activateFirstItemCommand;
	}

	private static IObservable<bool> HasSingleItem(ISearchSource searchSource)
	{
		return searchSource.Changes.ToCollection().Select(s => s.OfType<IActionableItem>().Count() == 1);
	}

	public IObservable<Unit> FirstItemActivated { get; }

	public ICommand ActivateFirstItemCommand { get; set; }

	public IObservable<bool> HasResults { get; }

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private void Reset()
	{
		IsSearchListVisible = false;
		SearchText = "";
	}
}
