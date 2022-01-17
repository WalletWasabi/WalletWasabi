using Nito.AsyncEx;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Caching.Memory;

public static class MemoryExtensions
{
	private static Dictionary<IMemoryCache, Dictionary<object, AsyncLock>> AsyncLocks { get; } = new Dictionary<IMemoryCache, Dictionary<object, AsyncLock>>();

	private static object AsyncLocksLock { get; } = new object();

	/// <param name="cache">Must be thread safe.</param>
	public static async Task<TItem> AtomicGetOrCreateAsync<TItem>(this IMemoryCache cache, object key, MemoryCacheEntryOptions options, Func<Task<TItem>> factory)
	{
		if (cache.TryGetValue(key, out TItem value))
		{
			return value;
		}

		AsyncLock asyncLock;
		lock (AsyncLocksLock)
		{
			// If we have no dic for the cache yet then create one.
			if (!AsyncLocks.TryGetValue(cache, out var cacheDic))
			{
				cacheDic = new Dictionary<object, AsyncLock>();
				AsyncLocks.Add(cache, cacheDic);
			}

			if (cacheDic.TryGetValue(key, out var al))
			{
				asyncLock = al;
			}
			else
			{
				asyncLock = new AsyncLock();
				cacheDic.Add(key, asyncLock);
			}
		}

		using (await asyncLock.LockAsync().ConfigureAwait(false))
		{
			if (!cache.TryGetValue(key, out value))
			{
				value = await factory.Invoke().ConfigureAwait(false);
				cache.Set(key, value, options);
				lock (AsyncLocksLock)
				{
					var cacheDic = AsyncLocks[cache];

					// Note that if a cache is disposed, then the cleanup will never happen. This should not cause normally issues, but keep in mind.
					// Cleanup the evicted asynclocks.
					foreach (var toRemove in cacheDic.Keys.Where(x => !cache.TryGetValue(x, out _)).ToList())
					{
						cacheDic.Remove(toRemove);
					}
				}
			}

			return value;
		}
	}
}
