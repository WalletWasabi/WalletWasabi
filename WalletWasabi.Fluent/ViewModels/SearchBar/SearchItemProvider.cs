using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public static class SearchItemProvider
{
	public static IObservable<SearchItem> GetSearchItems()
	{
		var data = NavigationManager.MetaData
			.Where(m => m.Searchable)
			.Select(m =>
			{
				var command = CreateCommand(m);
				var searchItem = new SearchItem(m.Title, m.Caption, command, m.Category ?? "No category", m.Keywords)
				{
					Icon = m.IconName,
				};
				return searchItem;
			});

		var items = data.ToObservable();

		return items;
	}

	private static ReactiveCommand<Unit, Unit> CreateCommand(NavigationMetaData navigationMetaData)
	{
		return ReactiveCommand.CreateFromTask(async () =>
		{
			var vm = await NavigationManager.MaterialiseViewModelAsync(navigationMetaData);
			if (vm is null)
			{
				return;
			}
			Navigate(vm.DefaultTarget).To(vm);
		});
	}

	private static INavigationStack<RoutableViewModel> Navigate(NavigationTarget currentTarget)
	{
		return currentTarget switch
		{
			NavigationTarget.HomeScreen => NavigationState.Instance.HomeScreenNavigation,
			NavigationTarget.DialogScreen => NavigationState.Instance.DialogScreenNavigation,
			NavigationTarget.FullScreen => NavigationState.Instance.FullScreenNavigation,
			NavigationTarget.CompactDialogScreen => NavigationState.Instance.CompactDialogScreenNavigation,
			_ => throw new NotSupportedException(),
		};
	}
}