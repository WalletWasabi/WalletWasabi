using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItem;
using WalletWasabi.Fluent.ViewModels.SearchBar.Settings;
using WalletWasabi.Fluent.ViewModels.Settings;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public class SettingsSource : ISearchItemSource
{
	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes => GetSettingsItems()
		.ToObservable()
		.ToObservableChangeSet(x => x.Key);

	private static IEnumerable<ISearchItem> GetSettingsItems()
	{
		var torSettings = new TorSettingsTabViewModel();
		var btSettings = new BitcoinTabSettingsViewModel();

		return new ISearchItem[]
		{
			new NonActionableSearchItem(new DarkThemeSetting(Services.UiConfig), "Dark theme", "Appearance", new List<string>(), "nav_settings_regular"){ IsDefault = false},
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.Autocopy), "Autocopy Bitcoin address", "Settings", new List<string>(), "nav_settings_regular")  { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.AutoPaste), "Autopaste Bitcoin address", "Settings", new List<string>(), "nav_settings_regular")  { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.HideOnClose), "Run in background when closed", "Settings", new List<string>() { "hide" }, "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.RunOnSystemStartup), "Run on system startup", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<TorSettingsTabViewModel, bool>(torSettings, b => b.UseTor, true), "Use Tor", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<TorSettingsTabViewModel, bool>(torSettings, b => b.TerminateTorOnExit, true), "Terminate Tor on exit", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<BitcoinTabSettingsViewModel, bool>(btSettings, b => b.StartLocalBitcoinCoreOnStartup), "Start local Bitcoin core on startup", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<BitcoinTabSettingsViewModel, bool>(btSettings, b => b.StopLocalBitcoinCoreOnShutdown), "Stop local Bitcoin core on shutdown", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
		};
	}
}