using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.SearchBar.Settings;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SettingsSearchSource : ReactiveObject, ISearchSource
{
	private readonly UiContext _uiContext;
	private readonly IApplicationSettings _applicationSettings;

	public SettingsSearchSource(UiContext uiContext, IObservable<string> query)
	{
		_uiContext = uiContext;
		_applicationSettings = uiContext.ApplicationSettings;

		var filter = query.Select(SearchSource.DefaultFilter);

		Changes = GetSettingsItems()
			.ToObservable()
			.ToObservableChangeSet(x => x.Key)
			.Filter(filter);
	}

	public IObservable<IChangeSet<ISearchItem, ComposedKey>> Changes { get; }

	private IEnumerable<ISearchItem> GetSettingsItems()
	{
		IObservable<bool> isVisible = this.WhenAnyValue(x => x._applicationSettings.StartLocalBitcoinCoreOnStartup);
		return new ISearchItem[]
		{
			new NonActionableSearchItem(_uiContext, Setting(x => x.DarkModeEnabled), "Dark mode", "Appearance", new List<string> { "Black", "White", "Theme", "Dark", "Light" }, "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(_uiContext, Setting(x => x.AutoCopy), "Auto copy addresses", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(_uiContext, Setting(x => x.AutoPaste), "Auto paste addresses", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(_uiContext, Setting(x => x.HideOnClose), "Run in background when closed", "Settings", new List<string>() { "hide", "tray" }, "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(_uiContext, Setting(x => x.RunOnSystemStartup), "Run Wasabi when computer starts", "Settings", new List<string>() { "startup", "boot" }, "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(_uiContext, Setting(x => x.UseTor), "Network anonymization (Tor)", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(_uiContext, Setting(x => x.TerminateTorOnExit), "Terminate Tor when Wasabi shuts down", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(_uiContext, Setting(x => x.StartLocalBitcoinCoreOnStartup), "Run Bitcoin Knots on startup", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
			new NonActionableSearchItem(_uiContext, Setting(x => x.StopLocalBitcoinCoreOnShutdown), "Stop Bitcoin Knots on shutdown", "Settings", new List<string>(), "nav_settings_regular", isVisible) { IsDefault = false },
			new NonActionableSearchItem(_uiContext, Setting(x => x.EnableGpu), "Enable GPU", "Settings", new List<string>(), "nav_settings_regular") { IsDefault = false },
		};
	}

	private Setting<IApplicationSettings, TProperty> Setting<TProperty>(Expression<Func<IApplicationSettings, TProperty>> selector)
	{
		return new Setting<IApplicationSettings, TProperty>(_applicationSettings, selector);
	}
}
