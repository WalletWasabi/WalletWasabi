using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Cache;

public class IdempotencyRequestCache
{
	public IdempotencyRequestCache(IMemoryCache cache)
	{
		ResponseCache = cache;
	}

	public delegate Task<TResponse> ProcessRequestDelegateAsync<TRequest, TResponse>(TRequest sender, CancellationToken cancellationToken);

	/// <summary>Timeout specifying how long a request response can stay in memory.</summary>
	private static MemoryCacheEntryOptions IdempotencyEntryOptions { get; } = new()
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
	};

	/// <summary>Guards <see cref="ResponseCache"/>.</summary>
	/// <remarks>Unfortunately, <see cref="CacheExtensions.GetOrCreate{TItem}(IMemoryCache, object, Func{ICacheEntry, TItem})"/> is not atomic.</remarks>
	/// <seealso href="https://github.com/dotnet/runtime/issues/36499"/>
	private object ResponseCacheLock { get; } = new();

	/// <remarks>Guarded by <see cref="ResponseCacheLock"/>.</remarks>
	private IMemoryCache ResponseCache { get; }

	/// <summary>
	/// Tries to add the cache key to cache to avoid other callers to add such a key in parallel.
	/// </summary>
	/// <returns><c>true</c> if the key was added to the cache, <c>false</c> otherwise.</returns>
	/// <remarks>Caller is responsible to ALWAYS set a result to <paramref name="responseTcs"/> even if an exception is thrown.</remarks>
	public bool TryAddKey<TRequest, TResponse>(TRequest cacheKey, MemoryCacheEntryOptions options, out TaskCompletionSource<TResponse> responseTcs)
		where TRequest : notnull
	{
		lock (ResponseCacheLock)
		{
			if (!ResponseCache.TryGetValue(cacheKey, out TaskCompletionSource<TResponse>? tcs))
			{
				responseTcs = new();
				ResponseCache.Set(cacheKey, responseTcs, options);

				return true;
			}

			responseTcs = tcs!;
			return false;
		}
	}

	/// <typeparam name="TRequest">
	/// <see langword="record"/>s are preferred as <see cref="object.GetHashCode"/>
	/// and <see cref="object.Equals(object?)"/> are generated for <see langword="record"/> types automatically.
	/// </typeparam>
	/// <typeparam name="TResponse">Type associated with <typeparamref name="TRequest"/>. The correspondence should be 1:1 mapping.</typeparam>
	public Task<TResponse> GetCachedResponseAsync<TRequest, TResponse>(TRequest request, ProcessRequestDelegateAsync<TRequest, TResponse> action, CancellationToken cancellationToken = default)
		where TRequest : notnull
	{
		return GetCachedResponseAsync(request, action, IdempotencyEntryOptions, cancellationToken);
	}

	/// <typeparam name="TRequest">
	/// <see langword="record"/>s are preferred as <see cref="object.GetHashCode"/>
	/// and <see cref="object.Equals(object?)"/> are generated for <see langword="record"/> types automatically.
	/// </typeparam>
	/// <typeparam name="TResponse">Type associated with <typeparamref name="TRequest"/>. The correspondence should be 1:1 mapping.</typeparam>
	public async Task<TResponse> GetCachedResponseAsync<TRequest, TResponse>(TRequest request, ProcessRequestDelegateAsync<TRequest, TResponse> action, MemoryCacheEntryOptions options, CancellationToken cancellationToken)
		where TRequest : notnull
	{
		bool callAction = TryAddKey(request, options, out TaskCompletionSource<TResponse>? responseTcs);

		if (callAction)
		{
			try
			{
				TResponse? result = await action(request, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
				responseTcs!.SetResult(result);
				return result;
			}
			catch (Exception e)
			{
				lock (ResponseCacheLock)
				{
					ResponseCache.Remove(request);
				}

				responseTcs!.SetException(e);

				// The exception will be thrown below at 'await' to avoid unobserved exception.
			}
		}

		return await responseTcs!.Task.ConfigureAwait(false);
	}

	/// <remarks>
	/// For testing purposes only.
	/// <para>Note that if there is a simultaneous request for the cache key, it is not stopped and its result is discarded.</para>
	/// </remarks>
	internal void Remove<TRequest>(TRequest cacheKey)
		where TRequest : notnull
	{
		lock (ResponseCacheLock)
		{
			ResponseCache.Remove(cacheKey);
		}
	}
}
