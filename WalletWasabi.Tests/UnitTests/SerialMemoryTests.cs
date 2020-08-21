using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	/// <summary>
	/// The tests in this collection are time-sensitive, therefore this test collection is run in a special way:
	/// Parallel-capable test collections will be run first (in parallel), followed by parallel-disabled test collections (run sequentially) like this one.
	/// </summary>
	/// <seealso href="https://xunit.net/docs/running-tests-in-parallel.html#parallelism-in-test-frameworks"/>
	[Collection("Serial unit tests collection")]
	public class SerialMemoryTests
	{
		[Fact]
		public async Task LockTestsAsync()
		{
			TimeSpan timeout = TimeSpan.FromSeconds(10);
			using SemaphoreSlim trigger = new SemaphoreSlim(0, 1);
			using SemaphoreSlim signal = new SemaphoreSlim(0, 1);

			async Task<string> WaitUntilTrigger(string argument)
			{
				signal.Release();
				if (!await trigger.WaitAsync(timeout))
				{
					throw new TimeoutException();
				}
				return argument;
			}

			var cache = new MemoryCache(new MemoryCacheOptions());

			var task0 = cache.AtomicGetOrCreateAsync(
				"key1",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) },
				() => WaitUntilTrigger("World!"));

			if (!await signal.WaitAsync(timeout))
			{
				throw new TimeoutException();
			}

			var task1 = cache.AtomicGetOrCreateAsync(
				"key1",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(60) },
				() => Task.FromResult("Should not change to this"));

			var task2 = cache.AtomicGetOrCreateAsync(
				"key1",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(60) },
				() => Task.FromResult("Should not change to this either"));

			var task3 = cache.AtomicGetOrCreateAsync(
				"key2",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(60) },
				() => Task.FromResult("Key2"));

			// Different key should immediately added.
			await task3.WithAwaitCancellationAsync(timeout);
			Assert.True(task3.IsCompletedSuccessfully);

			// Tasks are waiting for the factory method.
			Assert.False(task0.IsCompleted);
			Assert.False(task1.IsCompleted);
			Assert.False(task2.IsCompleted);

			// Let the factory method finish.
			trigger.Release();
			string result0 = await task0.WithAwaitCancellationAsync(timeout);
			Assert.Equal(TaskStatus.RanToCompletion, task0.Status);
			string result1 = await task1.WithAwaitCancellationAsync(timeout);
			string result2 = await task2.WithAwaitCancellationAsync(timeout);
			Assert.Equal(TaskStatus.RanToCompletion, task1.Status);
			Assert.Equal(TaskStatus.RanToCompletion, task2.Status);
			Assert.Equal(result0, result1);
			Assert.Equal(result0, result2);
		}
	}
}
