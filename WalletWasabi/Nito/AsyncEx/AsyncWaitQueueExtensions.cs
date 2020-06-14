using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nito.AsyncEx
{
	/// <summary>
	/// Provides extension methods for wait queues.
	/// </summary>
	public static class AsyncWaitQueueExtensions
	{
		/// <summary>
		/// Creates a new entry and queues it to this wait queue. If the cancellation token is already canceled, this method immediately returns a canceled task without modifying the wait queue.
		/// </summary>
		/// <param name="this">The wait queue.</param>
		/// <param name="mutex">A synchronization object taken while cancelling the entry.</param>
		/// <param name="token">The token used to cancel the wait.</param>
		/// <returns>The queued task.</returns>
		public static Task<T> Enqueue<T>(this IAsyncWaitQueue<T> @this, object mutex, CancellationToken token)
		{
			if (token.IsCancellationRequested)
			{
				return Task.FromCanceled<T>(token);
			}

			var ret = @this.Enqueue();
			if (!token.CanBeCanceled)
			{
				return ret;
			}

			var registration = token.Register(() =>
				{
					lock (mutex)
					{
						@this.TryCancel(ret, token);
					}
				},
				useSynchronizationContext: false);
			ret.ContinueWith(_ => registration.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
			return ret;
		}
	}
}
