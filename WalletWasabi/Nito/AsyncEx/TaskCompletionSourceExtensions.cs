using Nito.AsyncEx.Synchronous;
using System;
using System.Threading.Tasks;

namespace Nito.AsyncEx
{
	/// <summary>
	/// Provides extension methods for <see cref="TaskCompletionSource{TResult}"/>.
	/// </summary>
	public static class TaskCompletionSourceExtensions
	{
		/// <summary>
		/// Attempts to complete a <see cref="TaskCompletionSource{TResult}"/>, propagating the completion of <paramref name="task"/>.
		/// </summary>
		/// <typeparam name="TResult">The type of the result of the target asynchronous operation.</typeparam>
		/// <typeparam name="TSourceResult">The type of the result of the source asynchronous operation.</typeparam>
		/// <param name="this">The task completion source. May not be <c>null</c>.</param>
		/// <param name="task">The task. May not be <c>null</c>.</param>
		/// <returns><c>true</c> if this method completed the task completion source; <c>false</c> if it was already completed.</returns>
		public static bool TryCompleteFromCompletedTask<TResult, TSourceResult>(this TaskCompletionSource<TResult> @this, Task<TSourceResult> task) where TSourceResult : TResult
		{
			if (@this is null)
			{
				throw new ArgumentNullException(nameof(@this));
			}

			if (task is null)
			{
				throw new ArgumentNullException(nameof(task));
			}

			if (task.IsFaulted)
			{
				return @this.TrySetException(task.Exception.InnerExceptions);
			}

			if (task.IsCanceled)
			{
				try
				{
					task.WaitAndUnwrapException();
				}
				catch (OperationCanceledException exception)
				{
					var token = exception.CancellationToken;
					return token.IsCancellationRequested ? @this.TrySetCanceled(token) : @this.TrySetCanceled();
				}
			}
			return @this.TrySetResult(task.Result);
		}

		/// <summary>
		/// Creates a new TCS for use with async code, and which forces its continuations to execute asynchronously.
		/// </summary>
		/// <typeparam name="TResult">The type of the result of the TCS.</typeparam>
		public static TaskCompletionSource<TResult> CreateAsyncTaskSource<TResult>()
		{
			return new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		}
	}
}
