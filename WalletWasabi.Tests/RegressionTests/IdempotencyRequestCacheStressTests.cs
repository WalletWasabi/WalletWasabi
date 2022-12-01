using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Cache;
using WalletWasabi.Logging;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests;

/// <summary>
/// Stress tests for <see cref="IdempotencyRequestCache"/>.
/// </summary>
public class IdempotencyRequestCacheStressTests
{
	private const int TotalIterationCount = 50_000_000;

	[Fact]
	public void GetCachedResponseStressTest()
	{
		using MemoryCache memoryCache = new(new MemoryCacheOptions());
		MemoryCacheEntryOptions cacheOptions = new()
		{
			Size = 10,
			SlidingExpiration = TimeSpan.FromMilliseconds(50)
		};

		IdempotencyRequestCache requestCache = new(memoryCache);
		StateObject stateObject = new();

		RunTest(stateObject, taskProvider: (i) => requestCache.GetCachedResponseAsync(i % 2, stateObject.CounterTaskAsync));
	}

	private void RunTest(StateObject stateObject, Func<int, Task<string>> taskProvider)
	{
		Task[] tasks = new Task[TotalIterationCount];

		for (int i = 0; i < tasks.Length; i++)
		{
			if (i % 1000 == 0)
			{
				if (stateObject.TestResult == 0)
				{
					Debug.WriteLine($"#{i}: Failure detected!");
					break;
				}
				else
				{
					Debug.WriteLine($"#{i}");
				}
			}

			Task<string> t = taskProvider.Invoke(i % 2);
			tasks[i] = t;
		}

		if (stateObject.TestResult == 1)
		{
			Task.WhenAll(tasks);
		}

		Assert.Equal(1, stateObject.TestResult);
	}

	private class StateObject
	{
		private long[] _counters = new long[2];

		private long _testResult = 1;

		/// <summary>1 ~ success, 0 ~ error.</summary>
		public long TestResult
		{
			get => Interlocked.Read(ref _testResult);
			set => _ = Interlocked.Exchange(ref _testResult, value);
		}

		public async Task<string> CounterTaskAsync(int n, CancellationToken cancellationToken)
		{
			if (TestResult == 1)
			{
				try
				{
					// This block of code just switches a counter from 0 -> 1 and then, after a short pause, from 1 -> 0.
					// For an external observer, it is supposed to do nothing.
					// But if this method is called in parallel, then the counter value can happen to be equal to 2!
					// That means that some two cache misses lead to two "get resource calls". That should not be possible.
					long currentValue = Interlocked.Increment(ref _counters[n]);
					Assert.Equal(1, currentValue);

					await Task.Delay(Random.Shared.Next(0, 1000), cancellationToken).ConfigureAwait(false);

					currentValue = Interlocked.Decrement(ref _counters[n]);
					Assert.Equal(0, currentValue);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					TestResult = 0;
					throw;
				}
			}

			return "NotImportant";
		}
	}
}
