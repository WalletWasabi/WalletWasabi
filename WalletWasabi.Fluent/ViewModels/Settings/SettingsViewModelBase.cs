using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Timers;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public abstract class SettingsViewModelBase : ViewModelBase
	{
		private static string _configFilePath = "";

		protected SettingsViewModelBase(Global global)
		{
			_configFilePath = global.Config.FilePath;

			SaveSubject = new Subject<Config>();
			SaveSubject
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Throttle(TimeSpan.FromMilliseconds(1000))
				.Where(x => !global.Config.AreDeepEqual(x))
				.Subscribe(x => x.ToFile());
		}

		private static object ConfigLock { get; } = new ();

		private Subject<Config> SaveSubject { get; }

		protected void Save()
		{
			if (Validations.Any)
			{
				return;
			}

			var config = new Config(_configFilePath);

			Dispatcher.UIThread.PostLogException(
				() =>
				{
					lock (ConfigLock)
					{
						config.LoadFile();
						EditConfigOnSave(config);
						SaveSubject.OnNext(config);

						// IsModified = !Global.Config.AreDeepEqual(config);
					}
				});
		}

		protected abstract void EditConfigOnSave(Config config);
	}
}