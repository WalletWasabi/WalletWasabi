using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(
	Title = "Privacy",
	Caption = "Manage privacy settings",
	Order = 1,
	Category = "Settings",
	Keywords = new[] { "Settings", "Privacy", "Minimal", "Medium", "Strong", "Anonymity", "Level" },
	IconName = "settings_privacy_regular")]
public partial class PrivacySettingsTabViewModel : SettingsTabViewModelBase
{
	[AutoNotify] private int _minAnonScoreTarget;
	[AutoNotify] private int _maxAnonScoreTarget;

	public PrivacySettingsTabViewModel()
	{
	}

	protected override void EditConfigOnSave(Config config)
	{
	}
}
