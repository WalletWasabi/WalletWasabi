using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Timers;
using Avalonia.Threading;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public abstract class SettingsViewModelBase : ViewModelBase, IDisposable
	{
		private readonly string _configFilePath;

		protected SettingsViewModelBase(Global global)
		{
			_configFilePath = global.Config.FilePath;

			SaveTimer = new Timer(1000);
			SaveTimer.Elapsed += Save;
			SaveTimer.AutoReset = false;
		}

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

			var config = new Config(_configFilePath);

			// var subject = new Subject<Config>();
			// subject
			// 	.Throttle(TimeSpan.FromMilliseconds(1000))
			// 	.Subscribe(x => x.ToFile());
			//
			// subject.OnNext(config);
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

		public virtual void Dispose()
		{
			SaveTimer.Dispose();
		}
	}
}