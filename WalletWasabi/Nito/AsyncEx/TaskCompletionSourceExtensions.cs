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
		/// Creates a new TCS for use with async code, and which forces its continuations to execute asynchronously.
		/// </summary>
		/// <typeparam name="TResult">The type of the result of the TCS.</typeparam>
		public static TaskCompletionSource<TResult> CreateAsyncTaskSource<TResult>()
		{
			return new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		}
	}
}
