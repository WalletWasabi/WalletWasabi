using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;

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

	private IMemoryCache ResponseCache { get; }

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
		bool callAction = false;
		TaskCompletionSource<TResponse>? responseTcs;

		if (!ResponseCache.TryGetValue(request, out responseTcs))
		{
			callAction = true;
			responseTcs = new();
			ResponseCache.Set(request, responseTcs, options);
		}

		if (callAction)
		{
			try
			{
				TResponse? result = await action(request, cancellationToken).WithAwaitCancellationAsync(cancellationToken).ConfigureAwait(false);
				responseTcs!.SetResult(result);
				return result;
			}
			catch (Exception e)
			{
				ResponseCache.Remove(request);

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
		ResponseCache.Remove(cacheKey);
	}
}
