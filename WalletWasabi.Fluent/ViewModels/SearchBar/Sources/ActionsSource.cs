using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using WalletWasabi.Fluent.ViewModels.NavBar;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public class ActionsSource : ISearchItemSource
{
	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes => GetItemsFromMetadata()
		.ToObservable()
		.ToObservableChangeSet(x => x.Key);

	private static IEnumerable<ISearchItem> GetItemsFromMetadata()
	{
		return NavigationManager.MetaData
			.Where(m => m.Searchable)
			.Select(m =>
			{
				var onActivate = CreateOnActivateFunction(m);
				var searchItem = new ActionableItem(m.Title, m.Caption, onActivate, m.Category ?? "No category", m.Keywords)
				{
					Icon = m.IconName,
					IsDefault = true,
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
			else if (vm is TriggerCommandViewModel triggerCommandViewModel && triggerCommandViewModel.TargetCommand.CanExecute(default))
			{
				triggerCommandViewModel.TargetCommand.Execute(default);
			}
			else
			{
				RoutableViewModel.Navigate(vm.DefaultTarget).To(vm);
			}
		};
	}
}
