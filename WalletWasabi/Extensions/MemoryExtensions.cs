using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Caching.Memory
{
	public static class MemoryExtensions
	{
		private static Dictionary<object, AsyncLock> AsyncLocks { get; } = new Dictionary<object, AsyncLock>();

		private static object AsyncLocksLock { get; } = new object();

		public static async Task<TItem> AtomicGetOrCreateAsync<TItem>(this IMemoryCache cache, object key, Func<ICacheEntry, Task<TItem>> factory)
		{
			if (cache.TryGetValue(key, out TItem value))
			{
				return value;
			}

			AsyncLock asyncLock;
			lock (AsyncLocksLock)
			{
				// Cleanup the evicted asynclocks first.
				foreach (var toRemove in AsyncLocks.Keys.Where(x => !cache.TryGetValue(x, out _)).ToList())
				{
					AsyncLocks.Remove(toRemove);
				}

				if (!AsyncLocks.TryGetValue(key, out asyncLock))
				{
					asyncLock = new AsyncLock();
					AsyncLocks.Add(key, asyncLock);
				}
			}

			using (await asyncLock.LockAsync().ConfigureAwait(false))
			{
				if (!cache.TryGetValue(key, out value))
				{
					value = await cache.GetOrCreateAsync(key, factory).ConfigureAwait(false);
				}

				return value;
			}
		}
	}
}
