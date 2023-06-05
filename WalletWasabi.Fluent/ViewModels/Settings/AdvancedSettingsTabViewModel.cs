using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Daemon;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(
	Title = "Advanced",
	Caption = "Manage advanced settings",
	Order = 2,
	Category = "Settings",
	Keywords = new[]
	{
			"Settings", "Advanced", "Enable", "GPU"
	},
	IconName = "settings_general_regular")]
public partial class AdvancedSettingsTabViewModel : SettingsTabViewModelBase
{
	[AutoNotify] private bool _enableGpu;

	public AdvancedSettingsTabViewModel()
	{
		_enableGpu = Services.PersistentConfig.EnableGpu;

		this.WhenAnyValue(x => x.EnableGpu)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Skip(1)
			.Subscribe(_ => Save());
	}

	protected override void EditConfigOnSave(PersistentConfig persistentConfig)
	{
		persistentConfig.EnableGpu = EnableGpu;
	}
}
