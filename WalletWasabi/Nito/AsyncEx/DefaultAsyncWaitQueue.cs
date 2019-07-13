using Nito.AsyncEx;
using Nito.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nito.AsyncEx
{
	/// <summary>
	/// The default wait queue implementation, which uses a double-ended queue.
	/// </summary>
	/// <typeparam name="T">The type of the results. If this is not needed, use <see cref="object"/>.</typeparam>
	[DebuggerDisplay("Count = {Count}")]
	[DebuggerTypeProxy(typeof(DefaultAsyncWaitQueue<>.DebugView))]
	public sealed class DefaultAsyncWaitQueue<T> : IAsyncWaitQueue<T>
	{
		private readonly Deque<TaskCompletionSource<T>> _queue = new Deque<TaskCompletionSource<T>>();

		private int Count => _queue.Count;

		bool IAsyncWaitQueue<T>.IsEmpty => Count == 0;

		Task<T> IAsyncWaitQueue<T>.Enqueue()
		{
			var tcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<T>();
			_queue.AddToBack(tcs);
			return tcs.Task;
		}

		void IAsyncWaitQueue<T>.Dequeue(T result)
		{
			_queue.RemoveFromFront().TrySetResult(result);
		}

		void IAsyncWaitQueue<T>.DequeueAll(T result)
		{
			foreach (var source in _queue)
			{
				source.TrySetResult(result);
			}

			_queue.Clear();
		}

		bool IAsyncWaitQueue<T>.TryCancel(Task task, CancellationToken cancellationToken)
		{
			for (int i = 0; i != _queue.Count; ++i)
			{
				if (_queue[i].Task == task)
				{
					_queue[i].TrySetCanceled(cancellationToken);
					_queue.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		void IAsyncWaitQueue<T>.CancelAll(CancellationToken cancellationToken)
		{
			foreach (var source in _queue)
			{
				source.TrySetCanceled(cancellationToken);
			}

			_queue.Clear();
		}

		[DebuggerNonUserCode]
		internal sealed class DebugView
		{
			private readonly DefaultAsyncWaitQueue<T> _queue;

			public DebugView(DefaultAsyncWaitQueue<T> queue)
			{
				_queue = queue;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public Task<T>[] Tasks
			{
				get
				{
					var result = new List<Task<T>>(_queue._queue.Count);
					foreach (var entry in _queue._queue)
					{
						result.Add(entry.Task);
					}

					return result.ToArray();
				}
			}
		}
	}
}
