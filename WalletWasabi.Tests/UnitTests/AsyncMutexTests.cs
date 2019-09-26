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
		private static async Task TestMutexConcurrencyAsync(AsyncMutex asyncMutex, AsyncMutex asyncMutex2)
		{
			// Concurrency test with the same AsyncMutex object.
			using (var phase1 = new AutoResetEvent(false))
			using (var phase2 = new AutoResetEvent(false))
			using (var phase3 = new AutoResetEvent(false))
			{
				// Acquire the Mutex with a background thread.
				var myTask = Task.Run(async () =>
				{
					using (await asyncMutex.LockAsync())
					{
						// Phase 1: signal that the mutex has been acquired.
						phase1.Set();

						// Phase 2: wait for exclusion.
						Assert.True(phase2.WaitOne(TimeSpan.FromSeconds(20)));
					}
					// Phase 3: release the mutex.
					phase3.Set();
				});

				// Phase 1: wait for the first Task to acquire the mutex.
				Assert.True(phase1.WaitOne(TimeSpan.FromSeconds(20)));

				// Phase 2: check mutual exclusion.
				using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
				{
					await Assert.ThrowsAsync<IOException>(async () =>
					{
						using (await asyncMutex2.LockAsync(cts.Token))
						{
							throw new InvalidOperationException("Mutex should not be acquired here.");
						};
					});
				}
				phase2.Set();

				// Phase 3: wait for release and acquire the mutex
				Assert.True(phase3.WaitOne(TimeSpan.FromSeconds(20)));

				using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
				{
					// We should get this immediately.
					using (await asyncMutex2.LockAsync())
					{
					};
				}

				Assert.True(myTask.IsCompletedSuccessfully);
			}
		}

		[Fact]
		public async Task AsyncMutexTestsAsync()
		{
			var mutexName1 = $"mutex1-{DateTime.Now.Ticks.ToString()}"; // Randomize the name to avoid system wide collisions.

			AsyncMutex asyncMutex = new AsyncMutex(mutexName1);

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

			var mutexName2 = $"mutex2-{DateTime.Now.Ticks.ToString()}";

			// Test that asynclock cancellation is going to throw IOException.
			var mutex = new AsyncMutex(mutexName2);
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
			var mutex1 = new AsyncMutex(mutexName2);
			using (await mutex1.LockAsync())
			{
				using (var cts = new CancellationTokenSource(100))
				{
					var mutex2 = new AsyncMutex(mutexName2);
					await Assert.ThrowsAsync<IOException>(async () =>
					{
						using (await mutex2.LockAsync(cts.Token))
						{
						}
					});
				}
			}

			// Concurrency test with the same AsyncMutex object.
			await TestMutexConcurrencyAsync(asyncMutex, asyncMutex);

			// Concurrency test with different AsyncMutex object but same name.
			AsyncMutex asyncMutex2 = new AsyncMutex(mutexName1);
			await TestMutexConcurrencyAsync(asyncMutex, asyncMutex2);
		}
	}
}
;
