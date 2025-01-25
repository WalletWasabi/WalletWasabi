using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[AppLifetime]
[NavigationMetaData(
	Title = "General",
	Caption = "Manage general settings",
	Order = 0,
	Category = "Settings",
	Keywords = new[]
	{
			"Settings", "General", "Bitcoin", "Dark", "Mode", "Run", "Wasabi", "Computer", "System", "Start", "Background", "Close",
			"Auto", "Copy", "Paste", "Addresses", "Custom", "Change", "Address", "Fee", "Display", "Format", "BTC", "sats"
	},
	IconName = "settings_general_regular")]
public partial class GeneralSettingsTabViewModel : RoutableViewModel
{
	public GeneralSettingsTabViewModel(IApplicationSettings settings)
	{
		Settings = settings;
	}

	public bool IsReadOnly => Settings.IsOverridden;

	public IApplicationSettings Settings { get; }
}
