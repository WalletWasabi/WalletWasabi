using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace WalletWasabi.WabiSabi.Crypto
{
	public class UnboundedChannel<T>
	{
		private readonly ConcurrentQueue<T> _queue = new();
		private readonly ConcurrentQueue<TaskCompletionSource<T>> _waitingQueue = new();

		public void Send(T item)
		{
			if (_waitingQueue.TryDequeue(out var tcs))
			{
				tcs.TrySetResult(item);
				return;
			}

			_queue.Enqueue(item);
		}

		public Task<T> TakeAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			if (_queue.TryDequeue(out var item))
			{
				return Task.FromResult(item);
			}

			var tcs = new TaskCompletionSource<T>();
			_waitingQueue.Enqueue(tcs);

			using (cancellationToken.Register(() => tcs.TrySetCanceled()))
			return tcs.Task;
		}
	}
}
