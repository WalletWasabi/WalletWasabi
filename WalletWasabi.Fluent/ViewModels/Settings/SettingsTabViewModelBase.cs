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

		protected SettingsTabViewModelBase(Config config, UiConfig uiConfig)
		{
			ConfigOnOpen = new Config(config.FilePath);
			ConfigOnOpen.LoadFile();

			UiConfigOnOpen = new UiConfig(uiConfig.FilePath);
			UiConfigOnOpen.LoadFile();
		}

		public static event EventHandler<RestartNeededEventArgs>? RestartNeeded;

		public static Config? ConfigOnOpen { get; set; }

		public static UiConfig? UiConfigOnOpen { get; set; }

		private static object ConfigLock { get; } = new ();

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

							IsRestartNeeded();
						}
					}
					catch (Exception ex)
					{
						Logger.LogDebug(ex);
					}
				});
		}

		protected abstract void EditConfigOnSave(Config config);

		protected static void IsRestartNeeded(object? darkMode = null)
		{
			// When Avalonia improved the theme switching no need to check for UI Config changes,
			// because we can switch runtime, and it will be unnecessary to show the restart message.
			// TODO: Is theme switching without UI freeze working?

			if (UiConfigOnOpen is null || ConfigOnOpen is null)
			{
				return;
			}

			var currentConfig = new Config(ConfigOnOpen.FilePath);
			currentConfig.LoadFile();

			var currentUiConfig = new UiConfig(UiConfigOnOpen.FilePath);
			currentUiConfig.LoadFile();

			bool uiConfigChanged;
			if (darkMode is not null)
			{
				uiConfigChanged = UiConfigOnOpen.DarkModeEnabled != (bool) darkMode;
			}
			else
			{
				uiConfigChanged = !UiConfigOnOpen.AreDeepEqual(currentUiConfig);
			}

			var configChanged = !ConfigOnOpen.AreDeepEqual(currentConfig);

			RestartNeeded?.Invoke(
				typeof(SettingsTabViewModelBase),
				new RestartNeededEventArgs
				{
					IsRestartNeeded = uiConfigChanged || configChanged
				});
		}
	}
}