using System.Threading;
using System.Threading.Tasks;

namespace Nito.AsyncEx;

/// <summary>
/// A collection of cancelable <see cref="TaskCompletionSource{T}"/> instances. Implementations must assume the caller is holding a lock.
/// </summary>
/// <typeparam name="T">The type of the results. If this is not needed, use <see cref="object"/>.</typeparam>
public interface IAsyncWaitQueue<T>
{
	/// <summary>
	/// Gets a value indicating whether the queue is empty.
	/// </summary>
	bool IsEmpty { get; }

	/// <summary>
	/// Creates a new entry and queues it to this wait queue. The returned task must support both synchronous and asynchronous waits.
	/// </summary>
	/// <returns>The queued task.</returns>
	Task<T> Enqueue();

	/// <summary>
	/// Removes a single entry in the wait queue and completes it. This method may only be called if <see cref="IsEmpty"/> is <c>false</c>. The task continuations for the completed task must be executed asynchronously.
	/// </summary>
	/// <param name="result">The result used to complete the wait queue entry.</param>
	void Dequeue(T result);

	/// <summary>
	/// Attempts to remove an entry from the wait queue and cancels it. The task continuations for the completed task must be executed asynchronously.
	/// </summary>
	/// <param name="task">The task to cancel.</param>
	/// <param name="cancellationToken">The cancellation token to use to cancel the task.</param>
	bool TryCancel(Task task, CancellationToken cancellationToken);
}
