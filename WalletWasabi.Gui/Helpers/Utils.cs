using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace WalletWasabi.Gui
{
	public static class Utils
	{
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
