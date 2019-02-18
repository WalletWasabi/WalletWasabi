using Avalonia.Threading;
using System;
using System.IO;

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

		public static void PostLogException(this Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
		{
			try
			{
				dispatcher.Post(action, priority);
			}
			catch (Exception ex)
			{
				Logging.Logger.LogDebug<Dispatcher>(ex);
			}
		}
	}
}
