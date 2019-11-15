using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
				// Invokes every handler and makes sure the exceptions are caught. This is not possible
				// for async event handlers because they are fire-and-forget methods that require
				// being wrap around try-catch blocks
				foreach ( Delegate individualHandler in h.GetInvocationList() )
				{
					try
					{
						individualHandler.DynamicInvoke(sender, args);
					}
					catch(Exception ex)
					{
						Logger.LogError(ex);
					}
				}
			}
		}
	}
}