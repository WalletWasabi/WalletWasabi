using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public partial class SearchBarViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<SearchItemGroup> _groups;
	[AutoNotify] private bool _isSearchListVisible;
	[AutoNotify] private string _searchText = "";
	private readonly CompositeDisposable _disposables = new();

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
			.Replay(1)
			.RefCount();

		searchSource.Changes
			.Bind(out var results)
			.Subscribe()
			.DisposeWith(_disposables);

		var navigateToSearchCommand = ReactiveCommand.CreateFromTask(() => Task.FromResult(results.OfType<IActionableItem>().FirstOrDefault()?.OnExecution()));
		NavigateToSearchCommand = navigateToSearchCommand;
		var navigated = navigateToSearchCommand.ToSignal();
		Navigated = navigated;
		Navigated
			.Do(_ =>
			{
				IsSearchListVisible = false;
				SearchText = "";
			})
			.Subscribe()
			.DisposeWith(_disposables);
	}

	public IObservable<Unit> Navigated { get; }

	public ICommand NavigateToSearchCommand { get; set; }

	public IObservable<bool> HasResults { get; }

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;
}
