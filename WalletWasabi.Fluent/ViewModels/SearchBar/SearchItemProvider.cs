using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
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
				var func = CreateFunc(m);
				var searchItem = new SearchItem(m.Title, m.Caption, func, m.Category ?? "No category", m.Keywords)
				{
					Icon = m.IconName
				};
				return searchItem;
			});

		var items = data.ToObservable();

		return items;
	}

	private static Func<Task> CreateFunc(NavigationMetaData navigationMetaData)
	{
		return async () =>
		{
			var vm = await NavigationManager.MaterialiseViewModelAsync(navigationMetaData);
			if (vm is null)
			{
				return;
			}

			Navigate(vm.DefaultTarget).To(vm);
		};
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