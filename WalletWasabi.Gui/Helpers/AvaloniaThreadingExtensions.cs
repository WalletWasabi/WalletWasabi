using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace Avalonia.Threading
{
	public static class AvaloniaThreadingExtensions
	{
		public static void PostLogException(this Dispatcher dispatcher, Func<Task> action, DispatcherPriority priority = DispatcherPriority.Normal)
		{
			dispatcher.Post(
				async () =>
				{
					try
					{
						await action();
					}
					catch (Exception ex)
					{
						Logger.LogDebug(ex);
					}
				},
				priority);
		}

		public static void PostLogException(this Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
		{
			dispatcher.Post(
				() =>
				{
					try
					{
						action();
					}
					catch (Exception ex)
					{
						Logger.LogDebug(ex);
					}
				},
				priority);
		}
	}
}
