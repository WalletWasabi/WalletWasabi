using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Nito.AsyncEx;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class AbandonedTasksTests
	{
		[Fact]
		public async Task AllFeeEstimateOrdersByTargetAsync()
		{
			using CancellationTokenSource cts = new();

			using CancellationTokenSource cts2 = new();

			AbandonedTasks ab = new();

			// Add tasks with the first cancellation.
			for (int i = 0; i < 40; i++)
			{
				ab.AddAndClearCompleted(BusyWorkerTask(cts));
			}

			// Add a task with the second cancellation.
			ab.AddAndClearCompleted(BusyWorkerTask(cts2));

			// Start waiting for all to finish.
			var waitAllTask = ab.WhenAllAsync();

			await Task.Delay(50);

			// Tasks should still running.
			Assert.False(waitAllTask.IsCompleted);

			// Cancel most of the Tasks.
			cts.Cancel();

			await Task.Delay(50);

			// One task should still running.
			Assert.False(waitAllTask.IsCompleted);

			// Try to await but is should not finish before the cancellation so we will get OperationCanceledException.
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await waitAllTask.WithAwaitCancellationAsync(50));

			// Ok now cancel the last Task.
			cts2.Cancel();

			await Task.Delay(50);

			// Now await will finish.
			await waitAllTask;

			static async Task BusyWorkerTask(CancellationTokenSource cts)
			{
				try
				{
					// Wait for the cancellation.
					await Task.Delay(-1, cts.Token).ConfigureAwait(false);
				}
				catch
				{
					// Do not throw OperationCanceledException.
				}

				// Throw something else to be able to distinguish.
				throw new InvalidOperationException();
			}
		}
	}
}
