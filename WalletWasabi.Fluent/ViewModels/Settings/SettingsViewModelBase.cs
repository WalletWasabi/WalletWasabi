using System;
using System.Timers;
using Avalonia.Threading;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public abstract class SettingsViewModelBase : ViewModelBase
	{
		protected SettingsViewModelBase(Global global)
		{
			Global = global;

			SaveTimer = new Timer(1000);
			SaveTimer.Elapsed += Save;
			SaveTimer.AutoReset = false;
		}

		public Global Global { get; }

		public Timer SaveTimer { get; }

		private static object ConfigLock { get; } = new();

		protected void RequestSave()
		{
			// This will prevent multiple save in a moment
			SaveTimer.Stop();
			SaveTimer.Start();
		}

		private void Save(object sender, ElapsedEventArgs e)
		{
			if (Validations.Any)
			{
				return;
			}

			var config = new Config(Global.Config.FilePath);

			Dispatcher.UIThread.PostLogException(
				() =>
				{
					lock (ConfigLock)
					{
						config.LoadFile();
						EditConfigOnSave(config);
						config.ToFile();

						// IsModified = !Global.Config.AreDeepEqual(config);
					}
				});
		}

		protected abstract void EditConfigOnSave(Config config);
	}
}