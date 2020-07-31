using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Text;
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
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(10));
					return Task.FromResult(ExpensiveComputation("World!"));
				}
			);

			var result1 = await cache2.AtomicGetOrCreateAsync(
				"the-same-other-key",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(10));
					return Task.FromResult(ExpensiveComputation("Lurking Wife!"));
				}
			);

			var result2 = await cache1.AtomicGetOrCreateAsync(
				"the-same-key",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(10));
					return Task.FromResult(ExpensiveComputation("World!"));
				}
			);
			Assert.Equal(result0, result2);
			Assert.Equal(2, invoked);

			// Make sure AtomicGetOrCreateAsync doesn't fail because another cache is disposed.
			cache2.Dispose();
			var result3 = await cache1.AtomicGetOrCreateAsync(
				"the-same-key",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(10));
					return Task.FromResult(ExpensiveComputation("Foo!"));
				}
			);
			Assert.Equal("Hello World!", result3);
			Assert.Equal(2, invoked);

			// Make sure chache2 call will fail.
			await Assert.ThrowsAsync<ObjectDisposedException>(async () => await cache2.AtomicGetOrCreateAsync(
					"the-same-key",
					(entry) =>
					{
						entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(10));
						return Task.FromResult(ExpensiveComputation("Foo!"));
					}
				));
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

			var result0 = await cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(10));
					return Task.FromResult(ExpensiveComputation("World!"));
				}
			);

			var result1 = await cache.AtomicGetOrCreateAsync(
				"key2",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(10));
					return Task.FromResult(ExpensiveComputation("Lurking Wife!"));
				}
			);

			var result2 = await cache.AtomicGetOrCreateAsync(
				"key1",
				(entry) =>
				{
					entry.SetAbsoluteExpiration(TimeSpan.FromMilliseconds(10));
					return Task.FromResult(ExpensiveComputation("World!"));
				}
			);

			Assert.Equal(result0, result2);
			Assert.NotEqual(result0, result1);
			Assert.Equal(2, invoked);
		}
	}
}
