using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Cache;
using WalletWasabi.Logging;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Extensions;

/// <summary>
/// Tests for <see cref="MemoryExtensions"/>.
/// </summary>
public class MemoryExtensionsTests
{
	[Fact]
	public void AtomicGetOrCreateStressTest()
	{
		using MemoryCache memoryCache = new(new MemoryCacheOptions());

		MemoryCacheEntryOptions cacheOptions = new()
		{
			Size = 10,
			SlidingExpiration = TimeSpan.FromMilliseconds(50)
		};

		long testResult = 0;
		long[] counters = new long[2];
		Task[] tasks = new Task[50_000_000];

		for (int i = 0; i < tasks.Length; i++)
		{
			Task<string> t = memoryCache.AtomicGetOrCreateAsync(i % 2, cacheOptions, () => CounterTask(i % 2));
			tasks[i] = t;
		}

		Task.WhenAll(tasks);
		Assert.Equal(0, Interlocked.Read(ref testResult));

		// If AtomicGetOrCreateAsync works correctly, this method cannot be called in parallel
		// and the counter value should change from 0 -> 1 and then from 1 -> 0.
		// However, it can be called IN PARALLEL on my machine!
		async Task<string> CounterTask(int n)
		{
			if (Interlocked.Read(ref testResult) == 0)
			{
				try
				{
					long currentValue = Interlocked.Increment(ref counters[n]);
					Assert.Equal(1, currentValue);

					await Task.Delay(Random.Shared.Next(0, 10)).ConfigureAwait(false);

					currentValue = Interlocked.Decrement(ref counters[n]);
					Assert.Equal(0, currentValue);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					Interlocked.Exchange(ref testResult, 1);
					throw;
				}
			}

			return "NotImportant";
		}
	}

	[Fact]
	public void GetCachedResponseStressTest()
	{
		using MemoryCache memoryCache = new(new MemoryCacheOptions());

		MemoryCacheEntryOptions cacheOptions = new()
		{
			Size = 10,
			SlidingExpiration = TimeSpan.FromMilliseconds(50)
		};

		long testResult = 0;
		long[] counters = new long[2];
		IdempotencyRequestCache requestCache = new(memoryCache);
		Task[] tasks = new Task[50_000_000];

		for (int i = 0; i < tasks.Length; i++)
		{
			Task<string> t = requestCache.GetCachedResponseAsync(i % 2, CounterTask);
			tasks[i] = t;
		}

		Task.WhenAll(tasks);
		Assert.Equal(0, Interlocked.Read(ref testResult));

		// If GetCachedResponseAsync works correctly, this method cannot be called in parallel
		// and the counter value should change from 0 -> 1 and then from 1 -> 0.
		// It works correctly for me.
		async Task<string> CounterTask(int n, CancellationToken cancellationToken)
		{
			if (Interlocked.Read(ref testResult) == 0)
			{
				try
				{
					long currentValue = Interlocked.Increment(ref counters[n]);
					Assert.Equal(1, currentValue);

					await Task.Delay(Random.Shared.Next(0, 10), cancellationToken).ConfigureAwait(false);

					currentValue = Interlocked.Decrement(ref counters[n]);
					Assert.Equal(0, currentValue);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					Interlocked.Exchange(ref testResult, 1);
					throw;
				}
			}

			return "NotImportant";
		}
	}
}
