using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nito.AsyncEx.Synchronous
{
	/// <summary>
	/// Provides synchronous extension methods for tasks.
	/// </summary>
	public static class TaskExtensions
	{
		/// <summary>
		/// Waits for the task to complete, unwrapping any exceptions.
		/// </summary>
		/// <typeparam name="TResult">The type of the result of the task.</typeparam>
		/// <param name="task">The task. May not be <c>null</c>.</param>
		/// <returns>The result of the task.</returns>
		public static TResult WaitAndUnwrapException<TResult>(this Task<TResult> task)
		{
			if (task is null)
			{
				throw new ArgumentNullException(nameof(task));
			}

			return task.GetAwaiter().GetResult();
		}
	}
}
