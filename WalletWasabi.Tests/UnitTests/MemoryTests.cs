using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class MemoryTests
	{
		[Fact]
		public async Task MultiplesCachesAsync()
		{
			var invoked = 0;
			string ExpensiveComputation(string argument)
			{
				invoked++;
				return "Hello " + argument;
			}

			var cache1 = new MemoryCache(new MemoryCacheOptions());
			var cache2 = new MemoryCache(new MemoryCacheOptions());

			var result0 = await cache1.AtomicGetOrCreateAsync(
				"the-same-key",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
					return Task.FromResult(ExpensiveComputation("World!"));
				});

			var result1 = await cache2.AtomicGetOrCreateAsync(
				"the-same-other-key",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
					return Task.FromResult(ExpensiveComputation("Lurking Wife!"));
				});

			var result2 = await cache1.AtomicGetOrCreateAsync(
				"the-same-key",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
					return Task.FromResult(ExpensiveComputation("World!"));
				});
			Assert.Equal(result0, result2);
			Assert.Equal(2, invoked);

			// Make sure AtomicGetOrCreateAsync doesn't fail because another cache is disposed.
			cache2.Dispose();
			var result3 = await cache1.AtomicGetOrCreateAsync(
				"the-same-key",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
					return Task.FromResult(ExpensiveComputation("Foo!"));
				});
			Assert.Equal("Hello World!", result3);
			Assert.Equal(2, invoked);

			// Make sure cache2 call will fail.
			await Assert.ThrowsAsync<ObjectDisposedException>(async () => await cache2.AtomicGetOrCreateAsync(
					"the-same-key",
					(entry) =>
					{
						entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
						return Task.FromResult(ExpensiveComputation("Foo!"));
					}));
			Assert.Equal(2, invoked);
		}

		[Fact]
		public async Task CacheBasicBehaviorAsync()
		{
			var invoked = 0;
			string ExpensiveComputation(string argument)
			{
				invoked++;
				return "Hello " + argument;
			}

			var cache = new MemoryCache(new MemoryCacheOptions());
			var expireKey1 = new CancellationTokenSource();

			var result0 = await cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
					entry.AddExpirationToken(new CancellationChangeToken(expireKey1.Token));
					return Task.FromResult(ExpensiveComputation("World!"));
				});

			var result1 = await cache.AtomicGetOrCreateAsync(
				"key2",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
					return Task.FromResult(ExpensiveComputation("Lurking Wife!"));
				});

			var result2 = await cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
					return Task.FromResult(ExpensiveComputation("World!"));
				});

			Assert.Equal(result0, result2);
			Assert.NotEqual(result0, result1);
			Assert.Equal(2, invoked);

			// Make sure key1 expired.
			expireKey1.Cancel();
			var result3 = await cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
					return Task.FromResult(ExpensiveComputation("Foo!"));
				});
			var result4 = await cache.AtomicGetOrCreateAsync(
				"key2",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromHours(10));
					return Task.FromResult(ExpensiveComputation("Bar!"));
				});
			Assert.Equal(result1, result4);
			Assert.NotEqual(result0, result3);
			Assert.Equal(3, invoked);
		}

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
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(60));
					return WaitUntilTrigger("World!");
				}
			);

			if (!await signal.WaitAsync(timeout))
			{
				throw new TimeoutException();
			}

			var task1 = cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(60));
					return Task.FromResult("Should not change to this");
				}
			);

			var task2 = cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(60));
					return Task.FromResult("Should not change to this either");
				}
			);

			var task3 = cache.AtomicGetOrCreateAsync(
				"key2",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(60));
					return Task.FromResult("Key2");
				}
			);

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

		[Fact]
		public async Task ExpirationTestsAsync()
		{
			var cache = new MemoryCache(new MemoryCacheOptions());

			var result0 = await cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(1));
					return Task.FromResult("This will be expired");
				}
			);

			await Task.Delay(1);

			var result1 = await cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(60));
					return Task.FromResult("Foo");
				}
			);

			var result2 = await cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(60));
					return Task.FromResult("Should not change to this");
				}
			);

			Assert.Equal("Foo", result2);
		}
	}
}
