using System;
using System.Net;
using Avalonia.Threading;
using NBitcoin;
using WalletWasabi.Gui;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Settings
{
	public abstract class SettingsViewModelBase : ViewModelBase
	{
		public Global Global { get; }

		protected SettingsViewModelBase(Global global)
		{
			Global = global;
		}

		private static object ConfigLock { get; } = new object();

		protected void Save()
		{
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