using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public static class SearchItemProvider
{
	public static IObservable<ISearchItem> GetSearchItems()
	{
		var items = NavigationManager.MetaData
			.Where(m => m.Searchable)
			.Select(m =>
			{
				var func = CreateFunc(m);
				var searchItem = new ActionableItem(m.Title, m.Caption, func, m.Category ?? "No category", m.Keywords)
				{
					Icon = m.IconName
				};
				return searchItem;
			});

		return items
			.Concat(new ISearchItem[]
			{
				new NonActionableSearchItem(new DarkThemeSelector(), "Dark theme", "Appearance",
					new[] {"Dark", "Light", "Theme", "Appearance", "Colors"},
					null)
			})
			.ToObservable();
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
			_ => throw new NotSupportedException()
		};
	}
}