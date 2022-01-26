using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class MemoryTests
{
	[Fact]
	public async Task AbsoluteExpirationRelativeToNowExpiresCorrectlyAsync()
	{
		// This should be buggy but it seems to work for us: https://github.com/alastairtree/LazyCache/issues/84
		using var cache = new MemoryCache(new MemoryCacheOptions());

		var result = await cache.AtomicGetOrCreateAsync(
			"key",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(20) },
			() => Task.FromResult("foo"));
		Assert.Equal("foo", result);

		result = await cache.AtomicGetOrCreateAsync(
			"key",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(20) },
			() => Task.FromResult("bar"));
		Assert.Equal("foo", result);

		// Do not use Task.Delay here as it seems to be less precise.
		Thread.Sleep(21);

		result = await cache.AtomicGetOrCreateAsync(
			"key",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(20) },
			() => Task.FromResult("bar"));
		Assert.Equal("bar", result);
	}

	[Fact]
	public async Task MultiplesCachesAsync()
	{
		var invoked = 0;
		string ExpensiveComputation(string argument)
		{
			invoked++;
			return "Hello " + argument;
		}

		using var cache1 = new MemoryCache(new MemoryCacheOptions());
		using var cache2 = new MemoryCache(new MemoryCacheOptions());

		var result0 = await cache1.AtomicGetOrCreateAsync(
			"the-same-key",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
			() => Task.FromResult(ExpensiveComputation("World!")));

		var result1 = await cache2.AtomicGetOrCreateAsync(
			"the-same-other-key",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
			() => Task.FromResult(ExpensiveComputation("Loving Wife!")));

		var result2 = await cache1.AtomicGetOrCreateAsync(
			"the-same-key",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
			() => Task.FromResult(ExpensiveComputation("World!")));
		Assert.Equal(result0, result2);
		Assert.Equal(2, invoked);

		// Make sure AtomicGetOrCreateAsync doesn't fail because another cache is disposed.
		cache2.Dispose();
		var result3 = await cache1.AtomicGetOrCreateAsync(
			"the-same-key",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
			() => Task.FromResult(ExpensiveComputation("Foo!")));
		Assert.Equal("Hello World!", result3);
		Assert.Equal(2, invoked);

		// Make sure cache2 call will fail.
		await Assert.ThrowsAsync<ObjectDisposedException>(async () => await cache2.AtomicGetOrCreateAsync(
				"the-same-key",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
			() => Task.FromResult(ExpensiveComputation("Foo!"))));
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

		using var cache = new MemoryCache(new MemoryCacheOptions());
		using var expireKey1 = new CancellationTokenSource();

		var options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) };
		options.AddExpirationToken(new CancellationChangeToken(expireKey1.Token));
		var result0 = await cache.AtomicGetOrCreateAsync(
			"key1",
			options,
			() => Task.FromResult(ExpensiveComputation("World!")));

		var result1 = await cache.AtomicGetOrCreateAsync(
			"key2",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
			() => Task.FromResult(ExpensiveComputation("Loving Wife!")));

		var result2 = await cache.AtomicGetOrCreateAsync(
			"key1",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
			() => Task.FromResult(ExpensiveComputation("World!")));

		Assert.Equal(result0, result2);
		Assert.NotEqual(result0, result1);
		Assert.Equal(2, invoked);

		// Make sure key1 expired.
		expireKey1.Cancel();
		var result3 = await cache.AtomicGetOrCreateAsync(
			"key1",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
			() => Task.FromResult(ExpensiveComputation("Foo!")));

		var result4 = await cache.AtomicGetOrCreateAsync(
			"key2",
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
			() => Task.FromResult(ExpensiveComputation("Bar!")));

		Assert.Equal(result1, result4);
		Assert.NotEqual(result0, result3);
		Assert.Equal(3, invoked);
	}

	[Fact]
	public async Task ExpirationTestsAsync()
	{
		const string Key = "key";

		const string Value1 = "This will be expired";
		const string Value2 = "Foo";
		const string Value3 = "Should not change to this";

		using var cache = new MemoryCache(new MemoryCacheOptions());

		// First value should expire in 20 ms.
		string result0 = await cache.AtomicGetOrCreateAsync(
			Key,
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(20) },
			() => Task.FromResult(Value1));

		Stopwatch stopwatch = Stopwatch.StartNew();

		// Wait 30 ms to let first value expire.
		await Task.Delay(30);

		// Measure how long we have waited.
		long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

		// Set Value2 to be in the cache.
		string result1 = await cache.AtomicGetOrCreateAsync(
			Key,
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) },
			() => Task.FromResult(Value2));

		// Value3 is not supposed to be used as Value2 could not expire.
		string result2 = await cache.AtomicGetOrCreateAsync(
			Key,
			new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) },
			() => Task.FromResult(Value3));

		if (result2 != Value2)
		{
			Assert.False(
				true,
				$"{nameof(result2)} value is '{result2}' instead of '{Value2}'. Debug info: Wait time was: {elapsedMilliseconds} ms. Previous values: {nameof(result0)}='{result0}', {nameof(result1)}='{result1}'");
		}
	}

	[Fact]
	public async Task CacheTaskTestAsync()
	{
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var greatCalled = 0;
		var leeCalled = 0;

		async Task<string> Greet(MemoryCache cache, string who) =>
			await cache.AtomicGetOrCreateAsync(
				$"{nameof(Greet)}{who}",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromTicks(1) }, // expires really soon
				() =>
				{
					greatCalled++;
					return Task.FromResult($"Hello Mr. {who}");
				});

		async Task<string> GreetMrLee(MemoryCache cache) =>
			await cache.AtomicGetOrCreateAsync(
				"key1",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
				() =>
				{
					leeCalled++;
					return Greet(cache, "Lee");
				});

		for (var i = 0; i < 10; i++)
		{
			await GreetMrLee(cache);
		}

		Assert.Equal(1, greatCalled);
		Assert.Equal(1, leeCalled);
	}

	[Fact]
	public async Task LockTestsAsync()
	{
		TimeSpan timeout = TimeSpan.FromSeconds(10);
		using SemaphoreSlim trigger = new(0, 1);
		using SemaphoreSlim signal = new(0, 1);

		async Task<string> WaitUntilTrigger(string argument)
		{
			signal.Release();
			if (!await trigger.WaitAsync(timeout))
			{
				throw new TimeoutException();
			}
			return argument;
		}

		using var cache = new MemoryCache(new MemoryCacheOptions());

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
