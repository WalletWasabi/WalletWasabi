namespace System.Threading.Tasks
{
	public static class TaskExtensions
	{
		/// <summary>
		/// UNSAFE! You are cancelling the wait on the callback of the original Task, not cancelling the operation itself.
		/// https://stackoverflow.com/questions/14524209/what-is-the-correct-way-to-cancel-an-async-operation-that-doesnt-accept-a-cance/14524565#14524565
		/// </summary>
		public static async Task<T> WithAwaitCancellationAsync<T>(this Task<T> me, CancellationToken cancel, int waitForGracefulTermination = 0)
		{
			// The tasck completion source.
			var tcs = new TaskCompletionSource<bool>();

			// Register with the cancellation token.
			using (cancel.Register(
						s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
			{
				// If the task waited on is the cancellation token...
				if (me != await Task.WhenAny(me, tcs.Task).ConfigureAwait(false))
				{
					if (waitForGracefulTermination <= 0)
					{
						throw new OperationCanceledException(cancel);
					}
					else
					{
						using (var cts = new CancellationTokenSource(waitForGracefulTermination))
						{
							return await me.WithAwaitCancellationAsync(cts.Token).ConfigureAwait(false);
						}
					}
				}
			}

			// Wait for one or the other to complete.
			return await me.ConfigureAwait(false);
		}
	}
}
