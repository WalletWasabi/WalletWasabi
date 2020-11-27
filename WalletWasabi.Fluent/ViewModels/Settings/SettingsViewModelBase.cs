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
		protected SettingsViewModelBase(Global global)
		{
			Global = global;
		}

		public Global Global { get; }

		private static object ConfigLock { get; } = new ();

		protected void Save()
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

						if (!Global.Config.AreDeepEqual(config))
						{
							config.ToFile();
						}

						// IsModified = !Global.Config.AreDeepEqual(config);
					}
				});
		}

		protected abstract void EditConfigOnSave(Config config);
	}
}