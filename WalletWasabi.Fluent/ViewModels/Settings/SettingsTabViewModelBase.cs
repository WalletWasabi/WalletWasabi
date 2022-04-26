using System.Reactive.Concurrency;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Settings;

public abstract class SettingsTabViewModelBase : RoutableViewModel
{
	protected const int ThrottleTime = 500;

	protected SettingsTabViewModelBase()
	{
		ConfigOnOpen = new Config(Services.Config.FilePath);
		ConfigOnOpen.LoadFile();
	}

	public static event EventHandler<RestartNeededEventArgs>? RestartNeeded;

	public static Config? ConfigOnOpen { get; set; }

	private static object ConfigLock { get; } = new();

	protected void Save()
	{
		if (Validations.Any || ConfigOnOpen is null)
		{
			return;
		}

		var config = new Config(ConfigOnOpen.FilePath);

		RxApp.MainThreadScheduler.Schedule(
			() =>
			{
				try
				{
					lock (ConfigLock)
					{
						Config newConfig = EditConfigOnSave(config);
						newConfig.ToFile();

						IsRestartNeeded(config);
					}
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			});
	}

	protected abstract Config EditConfigOnSave(Config config);

	private static void IsRestartNeeded(Config configOnOpen)
	{
		var currentConfig = new Config(configOnOpen.FilePath);
		currentConfig.LoadFile();

		var configChanged = !configOnOpen.AreDeepEqual(currentConfig);

		RestartNeeded?.Invoke(
			typeof(SettingsTabViewModelBase),
			new RestartNeededEventArgs
			{
				IsRestartNeeded = configChanged
			});
	}
}
