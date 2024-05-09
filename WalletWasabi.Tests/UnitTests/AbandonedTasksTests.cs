using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Nito.AsyncEx;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class AbandonedTasksTests
{
	[Fact]
	public async Task AbandonedTasksTestsAsync()
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

		// Tasks should still be running.
		Assert.False(waitAllTask.IsCompleted);

		// Cancel most of the Tasks.
		cts.Cancel();

		await Task.Delay(50);

		// One task should still be running.
		Assert.False(waitAllTask.IsCompleted);

		// Try to await but it should not finish before the cancellation so we will get TimeoutException.
		await Assert.ThrowsAsync<TimeoutException>(async () => await waitAllTask.WaitAsync(TimeSpan.FromMilliseconds(50)));

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
