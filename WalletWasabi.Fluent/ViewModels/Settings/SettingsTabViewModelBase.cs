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

						IsRestartNeeded(ConfigOnOpen);
					}
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			});
	}

	/// <summary>
	/// Applies changes the user made in settings tabs.
	/// </summary>
	/// <param name="config">Current file config as stored in the config file.</param>
	/// <returns>Either the same reference or a new copy of the config with user settings changes.</returns>
	protected abstract Config EditConfigOnSave(Config config);

	/// <summary>
	/// Compares config when the app was started with the current config.
	/// Any change means that user needs to restart the app to apply the changes.
	/// </summary>
	private static void IsRestartNeeded(Config configOnOpen)
	{
		Config currentConfig = new(configOnOpen.FilePath);
		currentConfig.LoadFile();

		bool isRestartNeeded = !configOnOpen.AreDeepEqual(currentConfig);

		RestartNeeded?.Invoke(
			typeof(SettingsTabViewModelBase),
			new RestartNeededEventArgs
			{
				IsRestartNeeded = isRestartNeeded
			});
	}
}
