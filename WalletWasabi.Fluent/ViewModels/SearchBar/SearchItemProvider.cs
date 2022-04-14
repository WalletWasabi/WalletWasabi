using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public static class SearchItemProvider
{
	public static IObservable<ISearchItem> GetSearchItems()
	{
		return GetItemsFromMetadata()
			.ToObservable();
	}

	private static IEnumerable<ActionableItem> GetItemsFromMetadata()
	{
		return NavigationManager.MetaData
			.Where(m => m.Searchable)
			.Select(m =>
			{
				var onActivate = CreateOnActivateFunction(m);
				var searchItem = new ActionableItem(m.Title, m.Caption, onActivate, m.Category ?? "No category", m.Keywords)
				{
					Icon = m.IconName
				};
				return searchItem;
			});
	}

	private static Func<Task> CreateOnActivateFunction(NavigationMetaData navigationMetaData)
	{
		return async () =>
		{
			var vm = await NavigationManager.MaterialiseViewModelAsync(navigationMetaData);
			if (vm is null)
			{
				return;
			}

			if (vm is NavBarItemViewModel item && item.OpenCommand.CanExecute(default))
			{
				item.OpenCommand.Execute(default);
			}
			else
			{
				RoutableViewModel.Navigate(vm.DefaultTarget).To(vm);
			}
		};
	}
}
