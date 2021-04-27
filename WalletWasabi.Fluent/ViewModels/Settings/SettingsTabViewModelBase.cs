using System;
using System.Reactive.Concurrency;
using ReactiveUI;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Gui;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public abstract class SettingsTabViewModelBase : RoutableViewModel
	{
		protected const int ThrottleTime = 500;

		protected SettingsTabViewModelBase(Config config)
		{
			ConfigOnOpen = new Config(config.FilePath);
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

							IsRestartNeeded(ConfigOnOpen);
						}
					}
					catch (Exception ex)
					{
						Logger.LogDebug(ex);
					}
				});
		}

		protected abstract void EditConfigOnSave(Config config);

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
}
