using System.Collections.Generic;
using System.Reactive.Linq;
using DynamicData;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.SearchBar.Settings;
using WalletWasabi.Fluent.ViewModels.Settings;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

public class SettingsSource : ISearchItemSource
{
	private readonly SettingsPageViewModel _settingsPage;

	public SettingsSource(SettingsPageViewModel settingsPage)
	{
		_settingsPage = settingsPage;
	}

	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes => GetSettingsItems()
		.ToObservable()
		.ToObservableChangeSet(x => x.Key);

	private IEnumerable<ISearchItem> GetSettingsItems()
	{
		return new ISearchItem[]
		{
			new NonActionableSearchItem(new Setting<GeneralSettingsTabViewModel, bool>(_settingsPage.GeneralSettingsTab, x => x.DarkModeEnabled), "Dark mode", "Appearance", new List<string> { "Black", "White", "Theme", "Dark", "Light" }, "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<GeneralSettingsTabViewModel, bool>(_settingsPage.GeneralSettingsTab, b => b.AutoCopy), "Auto copy addresses", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<GeneralSettingsTabViewModel, bool>(_settingsPage.GeneralSettingsTab, b => b.AutoPaste), "Auto paste addresses", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<GeneralSettingsTabViewModel, bool>(_settingsPage.GeneralSettingsTab, b => b.HideOnClose), "Run in background when closed", "Settings", new List<string>() { "hide", "tray" }, "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<GeneralSettingsTabViewModel, bool>(_settingsPage.GeneralSettingsTab, b => b.RunOnSystemStartup), "Run Wasabi when computer starts", "Settings", new List<string>() { "startup", "boot" }, "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<GeneralSettingsTabViewModel, bool>(_settingsPage.GeneralSettingsTab, b => b.UseTor), "Network anonymization (Tor)", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<GeneralSettingsTabViewModel, bool>(_settingsPage.GeneralSettingsTab, b => b.TerminateTorOnExit), "Terminate Tor when Wasabi shuts down", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<BitcoinTabSettingsViewModel, bool>(_settingsPage.BitcoinTabSettings, b => b.StartLocalBitcoinCoreOnStartup), "Start local Bitcoin core on startup", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(new Setting<BitcoinTabSettingsViewModel, bool>(_settingsPage.BitcoinTabSettings, b => b.StopLocalBitcoinCoreOnShutdown), "Stop local Bitcoin core on shutdown", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
		};
	}
}
