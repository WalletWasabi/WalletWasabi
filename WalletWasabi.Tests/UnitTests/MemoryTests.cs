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
			Assert.Equal(2, invoked);        // <-------- this fails
		}
	}
}
