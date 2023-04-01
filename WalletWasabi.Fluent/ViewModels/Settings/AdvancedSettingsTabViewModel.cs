using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;

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
	[AutoNotify] private string? _model;
	[AutoNotify] private string? _apiKey;

	public AdvancedSettingsTabViewModel()
	{
		_enableGpu = Services.Config.EnableGpu;
		_model = Services.UiConfig.Model;
		_apiKey = Services.UiConfig.ApiKey;

		this.WhenAnyValue(x => x.EnableGpu)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Skip(1)
			.Subscribe(_ => Save());

		this.WhenAnyValue(x => x.Model)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.Model = x);

		this.WhenAnyValue(x => x.ApiKey)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Subscribe(x => Services.UiConfig.ApiKey = x);
	}

	protected override void EditConfigOnSave(Config config)
	{
		config.EnableGpu = EnableGpu;
	}
}
