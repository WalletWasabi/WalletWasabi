using System.Reactive.Concurrency;
using ReactiveUI;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Settings;

public abstract class SettingsTabViewModelBase : RoutableViewModel
{
	public const int ThrottleTime = 500;

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
						config.LoadFile();
						EditConfigOnSave(config);
						config.ToFile();

						OnConfigSaved();
					}
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
			});
	}

	protected abstract void EditConfigOnSave(Config config);

	private static void OnConfigSaved()
	{
		var isRestartNeeded = CheckIfRestartIsNeeded();

		RestartNeeded?.Invoke(
			typeof(SettingsTabViewModelBase),
			new RestartNeededEventArgs
			{
				IsRestartNeeded = isRestartNeeded
			});
	}

	public static bool CheckIfRestartIsNeeded()
	{
		if (ConfigOnOpen is null)
		{
			return false;
		}

		var currentConfig = new Config(ConfigOnOpen.FilePath);
		currentConfig.LoadFile();

		var isRestartNeeded = !ConfigOnOpen.AreDeepEqual(currentConfig);

		return isRestartNeeded;
	}
}
