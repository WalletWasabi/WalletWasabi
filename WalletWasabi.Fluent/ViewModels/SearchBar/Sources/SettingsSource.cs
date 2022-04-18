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
			new NonActionableSearchItem(new DarkThemeSetting(Services.UiConfig), "Dark mode", "Appearance", new List<string> { "Black", "White", "Theme" }, "nav_settings_regular"){ IsDefault = false},
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.Autocopy), "Auto copy addresses", "Settings", new List<string>(), "nav_settings_regular")  { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.AutoPaste), "Auto paste addresses", "Settings", new List<string>(), "nav_settings_regular")  { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.HideOnClose), "Run in background when closed", "Settings", new List<string>() { "hide", "tray" }, "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<UiConfig, bool>(Services.UiConfig, b => b.RunOnSystemStartup), "Run Wasabi when computer starts", "Settings", new List<string>() { "startup", "boot" }, "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<TorSettingsTabViewModel, bool>(torSettings, b => b.UseTor, true), "Network anonymization (Tor)", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<TorSettingsTabViewModel, bool>(torSettings, b => b.TerminateTorOnExit, true), "Terminate Tor when Wasabi shuts down", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<BitcoinTabSettingsViewModel, bool>(btSettings, b => b.StartLocalBitcoinCoreOnStartup), "Start local Bitcoin core on startup", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<BitcoinTabSettingsViewModel, bool>(btSettings, b => b.StopLocalBitcoinCoreOnShutdown), "Stop local Bitcoin core on shutdown", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
		};
	}
}