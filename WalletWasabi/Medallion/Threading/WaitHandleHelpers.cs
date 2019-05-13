using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Medallion.Threading
{
	internal static class WaitHandleHelpers
	{
		public static Task<bool> WaitOneAsync(this WaitHandle @this, TimeSpan timeout, CancellationToken cancellationToken)
		{
			if (@this == null)
				throw new ArgumentNullException("this");
			var timeoutMillis = timeout.ToInt32Timeout();

			return WaitOneAsync(@this, timeoutMillis, cancellationToken);
		}

		// based on http://www.thomaslevesque.com/2015/06/04/async-and-cancellation-support-for-wait-handles/
		private static async Task<bool> WaitOneAsync(WaitHandle handle, int timeoutMillis, CancellationToken cancellationToken)
		{
			RegisteredWaitHandle registeredHandle = null;
			CancellationTokenRegistration tokenRegistration = default;
			try
			{
				var taskCompletionSource = new TaskCompletionSource<bool>();
				registeredHandle = ThreadPool.RegisterWaitForSingleObject(
					handle,
					(state, timedOut) => ((TaskCompletionSource<bool>)state).TrySetResult(!timedOut),
					state: taskCompletionSource,
					millisecondsTimeOutInterval: timeoutMillis,
					executeOnlyOnce: true
				);
				tokenRegistration = cancellationToken.Register(
					state => ((TaskCompletionSource<bool>)state).TrySetCanceled(),
					state: taskCompletionSource
				);
				return await taskCompletionSource.Task;
			}
			finally
			{
				if (registeredHandle != null)
				{
					// this is different from the referenced site, but I think this is more correct:
					// the handle passed to unregister is a handle to be signaled, not the one to unregister
					// (that one is already captured by the registered handle). See
					// http://referencesource.microsoft.com/#mscorlib/system/threading/threadpool.cs,065408fc096354fd
					registeredHandle.Unregister(null);
				}
				tokenRegistration.Dispose();
			}
		}
	}
}
