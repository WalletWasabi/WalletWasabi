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
			.Concat(GetAdditionalItems())
			.ToObservable();
	}

	private static IEnumerable<ISearchItem> GetAdditionalItems()
	{
		return new ISearchItem[]
		{
			new NonActionableSearchItem(new DarkThemeSetting(Services.UiConfig), "Dark theme", "Appearance", new List<string>(), null){ IsDefault = false},
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.Autocopy), "Autocopy", "Settings", new List<string>(), null)  { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.AutoPaste), "Autopaste", "Settings", new List<string>(), null)  { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.HideOnClose), "Hide on close", "Settings", new List<string>(), null) { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.RunOnSystemStartup), "Run on system startup", "Settings", new List<string>(), null) { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.UseTor), "User Tor", "Settings", new List<string>(), null) { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.TerminateTorOnExit), "Terminate Tor on exit", "Settings", new List<string>(), null) { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.StartLocalBitcoinCoreOnStartup), "Start local Bitcoin core on startup", "Settings", new List<string>(), null) { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.StopLocalBitcoinCoreOnShutdown), "Start local Bitcoin core on shutdown", "Settings", new List<string>(), null) { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.JsonRpcServerEnabled), "Enable JSON-RPC Server", "Settings", new List<string>(), null) { IsDefault = false },
		};
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
			else
			{
				RoutableViewModel.Navigate(vm.DefaultTarget).To(vm);
			}
		};
	}
}