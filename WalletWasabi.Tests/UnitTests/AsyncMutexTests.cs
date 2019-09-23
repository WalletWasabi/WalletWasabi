using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class AsyncMutexTests
	{
		[Fact]
		public async Task AsyncMutexTestsAsync()
		{
			AsyncMutex asyncMutex = new AsyncMutex("mutex1");

			// Cannot be IDisposable because the pattern is like Nito's AsyncLock.
			Assert.False(asyncMutex is IDisposable);

			// Use the mutex two times after each other.
			using (await asyncMutex.LockAsync())
			{
				await Task.Delay(1);
			}

			using (await asyncMutex.LockAsync())
			{
				await Task.Delay(1);
			}

			// Release the Mutex from another thread.

			var disposable = await asyncMutex.LockAsync();

			var myThread = new Thread(new ThreadStart(() =>
			{
				disposable.Dispose();
			}));
			myThread.Start();
			myThread.Join();

			using (await asyncMutex.LockAsync())
			{
				await Task.Delay(1);
			}

			// Acquire the Mutex with a background thread.

			var myTask = Task.Run(async () =>
			{
				using (await asyncMutex.LockAsync())
				{
					await Task.Delay(3000);
				}
			});

			// Wait for the Task.Run to Acquire the Mutex.
			await Task.Delay(100);

			// Try to get the Mutex and save the time.
			DateTime timeOfstart = DateTime.Now;
			DateTime timeOfAcquired = default;

			using (await asyncMutex.LockAsync())
			{
				timeOfAcquired = DateTime.Now;
			};

			Assert.True(myTask.IsCompletedSuccessfully);

			var elapsed = timeOfAcquired - timeOfstart;
			Assert.InRange(elapsed, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(4000));

			// Standard Mutex test.
			int cnt = 0;
			List<int> numbers = new List<int>();
			var rand = new Random();
			async Task TestLockAsync()
			{
				using (await asyncMutex.LockAsync())
				{
					cnt++;

					await Task.Delay(rand.Next(5));
					numbers.Add(cnt);
				}
			}

			var tasks = new List<Task>();

			for (int i = 0; i < 100; i++)
			{
				var task = TestLockAsync();

				tasks.Add(task);
			}

			await Task.WhenAll(tasks);

			Assert.Equal(100, numbers.Count);
			for (int i = 1; i < 100; i++)
			{
				var prevnum = numbers[i - 1];
				var num = numbers[i];
				Assert.Equal(prevnum + 1, num);
			}

			// Test that asynclock cancellation is going to throw IOException.
			var mutex = new AsyncMutex("foo");
			using (await mutex.LockAsync())
			{
				using (var cts = new CancellationTokenSource(100))
				{
					await Assert.ThrowsAsync<IOException>(async () =>
					{
						using (await mutex.LockAsync(cts.Token))
						{
						}
					});
				}
			}

			// Test same mutex gets same asynclock.
			var mutex1 = new AsyncMutex("foo");
			using (await mutex1.LockAsync())
			{
				using (var cts = new CancellationTokenSource(100))
				{
					var mutex2 = new AsyncMutex("foo");
					await Assert.ThrowsAsync<IOException>(async () =>
					{
						using (await mutex2.LockAsync(cts.Token))
						{
						}
					});
				}
			}

			// Different AsyncMutex object but same name.
			AsyncMutex asyncMutex2 = new AsyncMutex("mutex1");

			// Acquire the first mutex with a background thread and hold it for a while.
			var myTask2 = Task.Run(async () =>
			{
				using (await asyncMutex.LockAsync())
				{
					await Task.Delay(3000);
				}
			});

			// Make sure the task started.
			await Task.Delay(100);

			timeOfstart = DateTime.Now;
			timeOfAcquired = default;
			// Now try to acquire another AsyncMutex object but with the same name!
			using (await asyncMutex2.LockAsync())
			{
				timeOfAcquired = DateTime.Now;
			}

			await myTask2;
			Assert.True(myTask2.IsCompletedSuccessfully);

			elapsed = timeOfAcquired - timeOfstart;
			Assert.InRange(elapsed, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(4000));
		}
	}
}
