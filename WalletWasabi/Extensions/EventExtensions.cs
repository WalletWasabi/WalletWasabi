using WalletWasabi.Logging;

namespace System
{
	public static class EventExtensions
	{
		public static void SafeInvoke<T>(this EventHandler<T> handler, object sender, T args)
		{
			var h = handler;
			if (h != null)
			{
				try
				{
					h(null, args);
				}
				catch(Exception ex)
				{
					Logger.LogInfo(ex);
				}
			}
		}
	}
}