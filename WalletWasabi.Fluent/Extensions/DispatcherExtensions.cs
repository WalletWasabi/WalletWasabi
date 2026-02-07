using Avalonia.Threading;
using System.Diagnostics;

namespace WalletWasabi.Fluent.Extensions;

public static class DispatcherExtensions
{
	public static void BreakOnInvalidThread(this Dispatcher dispatcher)
	{
#if DEBUG
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Debugger.Break();
		}
#endif
	}
}
