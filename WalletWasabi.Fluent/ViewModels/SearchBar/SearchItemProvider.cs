using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public static class SearchItemProvider
{
	public static IObservable<ISearchItem> GetSearchItems()
	{
		return GetItemsFromMetadata()
			.Concat(GetSettingsItems())
			.ToObservable();
	}

	private static IEnumerable<ISearchItem> GetSettingsItems()
	{
		return new ISearchItem[]
		{
			new NonActionableSearchItem(new DarkThemeSetting(Services.UiConfig), "Dark theme", "Appearance", new List<string>(), "nav_settings_regular"){ IsDefault = false},
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.Autocopy), "Autocopy Bitcoin address", "Settings", new List<string>(), "nav_settings_regular")  { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.AutoPaste), "Autopaste Bitcoin address", "Settings", new List<string>(), "nav_settings_regular")  { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.HideOnClose), "Hide on close", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.RunOnSystemStartup), "Run on system startup", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.UseTor), "User Tor", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.TerminateTorOnExit), "Terminate Tor on exit", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.StartLocalBitcoinCoreOnStartup), "Start local Bitcoin core on startup", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.StopLocalBitcoinCoreOnShutdown), "Start local Bitcoin core on shutdown", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<Config, bool>(Services.Config, b => b.JsonRpcServerEnabled), "Enable JSON-RPC Server", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
		};
	}

	private static IEnumerable<ActionableItem> GetItemsFromMetadata()
	{
		return NavigationManager.MetaData
			.Where(m => m.Searchable)
			.Select(m =>
			{
				var func = CreateFunc(m);
				var searchItem = new ActionableItem(m.Title, m.Caption, func, m.Category ?? "No category", m.Keywords)
				{
					Icon = m.IconName,
					IsDefault = true,
				};
				return searchItem;
			});
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

public class TransactionObserver
{
	public TransactionObserver()
	{
		var transactions = MessageBus.Current.Listen<TransactionsChangedMessage>()
			.Select(m => new TransactionSearchItem(m));
	}
}

public class TransactionSearchItem
{
	public TransactionSearchItem(TransactionsChangedMessage transactionsChangedMessage)
	{
		throw new NotImplementedException();
	}
}