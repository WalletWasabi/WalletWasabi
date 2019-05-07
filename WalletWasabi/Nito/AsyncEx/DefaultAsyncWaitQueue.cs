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
	/// <typeparam name="T">The type of the results. If this isn't needed, use <see cref="object"/>.</typeparam>
	[DebuggerDisplay("Count = {Count}")]
	[DebuggerTypeProxy(typeof(DefaultAsyncWaitQueue<>.DebugView))]
	public sealed class DefaultAsyncWaitQueue<T> : IAsyncWaitQueue<T>
	{
		private readonly Deque<TaskCompletionSource<T>> Queue = new Deque<TaskCompletionSource<T>>();

		private int Count => Queue.Count;

		bool IAsyncWaitQueue<T>.IsEmpty => Count == 0;

		Task<T> IAsyncWaitQueue<T>.Enqueue()
		{
			var tcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<T>();
			Queue.AddToBack(tcs);
			return tcs.Task;
		}

		void IAsyncWaitQueue<T>.Dequeue(T result)
		{
			Queue.RemoveFromFront().TrySetResult(result);
		}

		void IAsyncWaitQueue<T>.DequeueAll(T result)
		{
			foreach (var source in Queue)
			{
				source.TrySetResult(result);
			}

			Queue.Clear();
		}

		bool IAsyncWaitQueue<T>.TryCancel(Task task, CancellationToken cancellationToken)
		{
			for (int i = 0; i != Queue.Count; ++i)
			{
				if (Queue[i].Task == task)
				{
					Queue[i].TrySetCanceled(cancellationToken);
					Queue.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		void IAsyncWaitQueue<T>.CancelAll(CancellationToken cancellationToken)
		{
			foreach (var source in Queue)
			{
				source.TrySetCanceled(cancellationToken);
			}

			Queue.Clear();
		}

		[DebuggerNonUserCode]
		internal sealed class DebugView
		{
			private readonly DefaultAsyncWaitQueue<T> Queue;

			public DebugView(DefaultAsyncWaitQueue<T> queue)
			{
				Queue = queue;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public Task<T>[] Tasks
			{
				get
				{
					var result = new List<Task<T>>(Queue.Queue.Count);
					foreach (var entry in Queue.Queue)
					{
						result.Add(entry.Task);
					}

					return result.ToArray();
				}
			}
		}
	}
}
