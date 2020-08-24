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
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
				() => Task.FromResult(ExpensiveComputation("World!")));

			var result1 = await cache2.AtomicGetOrCreateAsync(
				"the-same-other-key",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
				() => Task.FromResult(ExpensiveComputation("Lurking Wife!")));

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

			var cache = new MemoryCache(new MemoryCacheOptions());
			var expireKey1 = new CancellationTokenSource();

			var options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) };
			options.AddExpirationToken(new CancellationChangeToken(expireKey1.Token));
			var result0 = await cache.AtomicGetOrCreateAsync(
				"key1",
				options,
				() => Task.FromResult(ExpensiveComputation("World!")));

			var result1 = await cache.AtomicGetOrCreateAsync(
				"key2",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(10) },
				() => Task.FromResult(ExpensiveComputation("Lurking Wife!")));

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
			var cache = new MemoryCache(new MemoryCacheOptions());

			var result0 = await cache.AtomicGetOrCreateAsync(
				"key1",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(1) },
				() => Task.FromResult("This will be expired"));

			await Task.Delay(1);

			var result1 = await cache.AtomicGetOrCreateAsync(
				"key1",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) },
				() => Task.FromResult("Foo"));

			var result2 = await cache.AtomicGetOrCreateAsync(
				"key1",
				new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) },
				() => Task.FromResult("Should not change to this"));

			Assert.Equal("Foo", result2);
		}

		[Fact]
		public async Task CacheTaskTestAsync()
		{
			var cache = new MemoryCache(new MemoryCacheOptions());
			var greatCalled = 0;
			var leeCalled = 0;

			async Task<string> Greet(string who) =>
				await cache.AtomicGetOrCreateAsync(
					$"{nameof(Greet)}{who}",
					new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromTicks(1) }, // expires really soon
					() =>
					{
						greatCalled++;
						return Task.FromResult($"Hello Mr. {who}");
					});

			async Task<string> GreetMrLee() =>
				await cache.AtomicGetOrCreateAsync(
					"key1",
					new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
					() =>
					{
						leeCalled++;
						return Greet("Lee");
					});

			for (var i = 0; i < 10; i++)
			{
				await GreetMrLee();
			}

			Assert.Equal(1, greatCalled);
			Assert.Equal(1, leeCalled);
		}
	}
}
