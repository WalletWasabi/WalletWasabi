using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;
using WalletWasabi.Fluent.ViewModels.SearchBar.Settings;
using WalletWasabi.Fluent.ViewModels.SearchBar.Sources;
using WalletWasabi.Models;

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
		var isEnabled = !_applicationSettings.IsOverridden;

		yield return new ContentSearchItem(content: Setting(selector: x => x.DarkModeEnabled), name: Lang.Resources.Settings_DarkModeEnabled_Name, category: SearchCategory.Settings, keywords: Lang.Keywords.ConstructKeywords("Settings_DarkModeEnabled_Keywords"), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 1 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.AutoCopy), name: Lang.Resources.Settings_AutoCopy_Name, category: SearchCategory.Settings, keywords: new List<string>(), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 2 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.AutoPaste), name: Lang.Resources.Settings_AutoPaste_Name, category: SearchCategory.Settings, keywords: new List<string>(), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 3 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.HideOnClose), name: Lang.Resources.Settings_HideOnClose_Name, category: SearchCategory.Settings, keywords: new List<string>() { "hide", "tray" }, icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 4 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.RunOnSystemStartup), name: Lang.Resources.Settings_RunOnSystemStartup_Name, category: SearchCategory.Settings, keywords: new List<string>() { "startup", "boot" }, icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 5 };
		yield return new ContentSearchItem(content: Setting(selector: x => x.EnableGpu), name: Lang.Resources.Settings_EnableGpu_Name, category: SearchCategory.Settings, keywords: new List<string>(), icon: "nav_settings_regular", isEnabled) { IsDefault = false, Priority = 6 };

		yield return ContentSearchItemNode.Create(
			searchSource: _uiContext.EditableSearchSource,
			setting: Setting(selector: x => x.UseTor),
			name: Lang.Resources.Settings_UseTor_Name,
			category: SearchCategory.Settings,
			isDefault: false,
			keywords: new List<string>(),
			icon: "nav_settings_regular",
			priority: 7,
			isEnabled,
			nestedItemConfiguration: new NestedItemConfiguration<TorMode>(
				isDisplayed: mode => mode != TorMode.Disabled,
				item: new ContentSearchItem(
					content: Setting(selector: x => x.TerminateTorOnExit),
					name: Lang.Resources.Settings_TerminateTorOnExit_Name,
					category: SearchCategory.Settings,
					keywords: new List<string>(),
					icon: "nav_settings_regular",
					isEnabled)
				{
					IsDefault = false,
					Priority = 8
				}));

		yield return ContentSearchItemNode.Create(
			searchSource: _uiContext.EditableSearchSource,
			setting: Setting(selector: x => x.StartLocalBitcoinCoreOnStartup),
			name: Lang.Resources.Settings_StartLocalBitcoinCoreOnStartup_Name,
			category: SearchCategory.Settings,
			isDefault: false,
			keywords: new List<string>(),
			icon: "nav_settings_regular",
			priority: 7,
			isEnabled,
			nestedItemConfiguration: new NestedItemConfiguration<bool>(
				isDisplayed: isVisible => isVisible,
				item: new ContentSearchItem(
					content: Setting(selector: x => x.StopLocalBitcoinCoreOnShutdown),
					name: Lang.Resources.Settings_StopLocalBitcoinCoreOnShutdown_Name,
					category: SearchCategory.Settings,
					keywords: new List<string>(),
					icon: "nav_settings_regular",
					isEnabled)
				{
					IsDefault = false,
					Priority = 8
				}));
	}

	private Setting<ApplicationSettings, TProperty> Setting<TProperty>(Expression<Func<ApplicationSettings, TProperty>> selector)
	{
		return new Setting<ApplicationSettings, TProperty>((ApplicationSettings)_applicationSettings, selector);
	}
}
