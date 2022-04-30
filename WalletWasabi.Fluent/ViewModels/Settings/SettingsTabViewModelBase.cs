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

		// Config stored in the file might be different than
		// the config the application was started with.
		Config currentFileConfig = new(ConfigOnOpen.FilePath);

		RxApp.MainThreadScheduler.Schedule(
			() =>
			{
				try
				{
					lock (ConfigLock)
					{
						Config newConfig = EditConfigOnSave(currentFileConfig);

						// Store settings changes.
						newConfig.ToFile();

						// Compare the config with which the app was started
						// and the current config in file. If there are modifications
						// we need the app to restart to apply the config changes.
						IsRestartNeeded(ConfigOnOpen);
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
		Config currentConfig = new(configOnOpen.FilePath);
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
