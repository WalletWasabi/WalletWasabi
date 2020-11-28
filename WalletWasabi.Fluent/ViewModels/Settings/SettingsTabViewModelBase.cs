using System;
using Avalonia.Threading;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class RestartNeededEventArgs : EventArgs
	{
		public bool IsRestartNeeded { get; init; }
	}

	public abstract class SettingsTabViewModelBase : ViewModelBase
	{
		protected const int ThrottleTime = 500;
		public static event EventHandler<RestartNeededEventArgs>? RestartNeeded;

		protected SettingsTabViewModelBase(Global global)
		{
			ConfigOnOpen = new Config(global.Config.FilePath);
			ConfigOnOpen.LoadFile();

			UiConfigOnOpen = new UiConfig(global.UiConfig.FilePath);
			UiConfigOnOpen.LoadFile();
		}

		public static Config? ConfigOnOpen { get; set; }
		public static UiConfig? UiConfigOnOpen { get; set; }
		private static object ConfigLock { get; } = new ();

		protected void Save(object darkMode = null!)
		{
			if (Validations.Any || ConfigOnOpen is null)
			{
				return;
			}

			var config = new Config(ConfigOnOpen.FilePath);

			Dispatcher.UIThread.PostLogException(
				() =>
				{
					lock (ConfigLock)
					{
						config.LoadFile();
						EditConfigOnSave(config);
						config.ToFile();

						IsRestartNeeded();
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
				uiConfigChanged = UiConfigOnOpen.DarkModeEnabled != (bool)darkMode;
			}
			else
			{
				uiConfigChanged = !UiConfigOnOpen.AreDeepEqual(currentUiConfig);
			}

			var configChanged = !ConfigOnOpen.AreDeepEqual(currentConfig);

			RestartNeeded?.Invoke(typeof(SettingsTabViewModelBase), new RestartNeededEventArgs{IsRestartNeeded = uiConfigChanged || configChanged});
		}
	}
}