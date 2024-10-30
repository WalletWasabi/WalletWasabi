using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Logging;
using System.Windows.Input;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Order = 0,
	Category = SearchCategory.Settings,
	Title = "GeneralSettingsTabViewModel_Title",
	Caption = "GeneralSettingsTabViewModel_Caption",
	Keywords = "GeneralSettingsTabViewModel_Keywords",
	IconName = "settings_general_regular")]
public partial class GeneralSettingsTabViewModel : RoutableViewModel
{
	[AutoNotify] private bool _runOnSystemStartup;

	public GeneralSettingsTabViewModel(IApplicationSettings settings)
	{
		Settings = settings;
		_runOnSystemStartup = settings.RunOnSystemStartup;

		StartupCommand = ReactiveCommand.Create(async () =>
		{
			try
			{
				settings.RunOnSystemStartup = RunOnSystemStartup;
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				RunOnSystemStartup = !RunOnSystemStartup;
				await ShowErrorAsync(
					Lang.Resources.GeneralSettingsTabViewModel_Title,
					Lang.Resources.GeneralSettingsTabViewModel_Error_CouldntSaveChanges_Message,
					Lang.Resources.GeneralSettingsTabViewModel_Error_CouldntSaveChanges_Caption);
			}
		});
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }

	public ICommand StartupCommand { get; }

	public IEnumerable<TorMode> TorModes =>
		Enum.GetValues(typeof(TorMode)).Cast<TorMode>();

	public IEnumerable<DisplayLanguage> DisplayLanguagesList => Enum.GetValues(typeof(DisplayLanguage)).Cast<DisplayLanguage>();
}
