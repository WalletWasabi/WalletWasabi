using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
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

			if (vm is DialogViewModelBase<Unit> dialog)
			{
				if (dialog is AddWalletPageViewModel)
				{
					MainViewModel.Instance.IsOobeBackgroundVisible = true;
					await dialog.NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);
					MainViewModel.Instance.IsOobeBackgroundVisible = false;
				}
				else
				{
					await dialog.NavigateDialogAsync(dialog, NavigationTarget.DialogScreen);
				}
			}
			else
			{
				Navigate(vm.DefaultTarget).To(vm);
			}
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
