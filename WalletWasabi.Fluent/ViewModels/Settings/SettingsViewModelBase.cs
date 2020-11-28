using System;
using Avalonia.Threading;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public class RestartNeedEventArgs : EventArgs
	{
		public bool IsRestartNeeded { get; set; }
	}

	public abstract class SettingsViewModelBase : ViewModelBase
	{
		public static event EventHandler<RestartNeedEventArgs>? RestartNeeded;

		protected SettingsViewModelBase(Global global)
		{
			ConfigOnOpen = new Config(global.Config.FilePath);
			ConfigOnOpen.LoadFile();
		}

		public static Config? ConfigOnOpen { get; set; }

		private static object ConfigLock { get; } = new ();

		protected void Save()
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

						var isRestartNeeded = !ConfigOnOpen.AreDeepEqual(config);

						if (isRestartNeeded)
						{
							config.ToFile();
						}

						RestartNeeded?.Invoke(this, new RestartNeedEventArgs{IsRestartNeeded = isRestartNeeded});
					}
				});
		}

		protected abstract void EditConfigOnSave(Config config);
	}
}