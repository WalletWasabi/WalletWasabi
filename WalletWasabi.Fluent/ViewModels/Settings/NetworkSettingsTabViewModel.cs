using System.Reactive.Linq;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Settings;

[NavigationMetaData(
	Title = "Network",
	Caption = "Manage network settings",
	Order = 2,
	Category = "Settings",
	Keywords = new[]
	{
			"Settings", "Network", "Encryption", "Tor", "Terminate", "Wasabi", "Shutdown", "SOCKS5", "Endpoint"
	},
	IconName = "settings_network_regular")]
public partial class NetworkSettingsTabViewModel : SettingsTabViewModelBase
{
	[AutoNotify] private bool _useTor;
	[AutoNotify] private bool _terminateTorOnExit;

	public NetworkSettingsTabViewModel()
	{
		_useTor = Services.Config.UseTor;
		_terminateTorOnExit = Services.Config.TerminateTorOnExit;

		this.WhenAnyValue(
				x => x.UseTor,
				x => x.TerminateTorOnExit)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Throttle(TimeSpan.FromMilliseconds(ThrottleTime))
			.Skip(1)
			.Subscribe(_ => Save());
	}

	protected override void EditConfigOnSave(Config config)
	{
		config.UseTor = UseTor;
		config.TerminateTorOnExit = TerminateTorOnExit;
	}
}
