using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Gui
{
	public static class Utils
	{
		public static string GetNextWalletName()
		{
			for (int i = 0; i < int.MaxValue; i++)
			{
				if (!File.Exists(Path.Combine(Global.WalletsDir, $"Wallet{i}.json")))
				{
					return $"Wallet{i}";
				}
			}

			throw new NotSupportedException("This is impossible.");
		}

		public static string GetNextHardwareWalletName(HardwareWalletInfo hwi)
		{
			for (int i = 0; i < int.MaxValue; i++)
			{
				var name = $"{hwi.Type}{i}";
				if (!File.Exists(Path.Combine(Global.WalletsDir, $"{name}.json")))
				{
					return name;
				}
			}

			throw new NotSupportedException("This is impossible.");
		}

		public static void PostLogException(this Dispatcher dispatcher, Func<Task> action, DispatcherPriority priority = DispatcherPriority.Normal)
		{
			dispatcher.Post(async () =>
			{
				try
				{
					await action();
				}
				catch (Exception ex)
				{
					Logging.Logger.LogDebug<Dispatcher>(ex);
				}
			}, priority);
		}

		public static void PostLogException(this Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
		{
			dispatcher.Post(() =>
			{
				try
				{
					action();
				}
				catch (Exception ex)
				{
					Logging.Logger.LogDebug<Dispatcher>(ex);
				}
			}, priority);
		}
	}
}
