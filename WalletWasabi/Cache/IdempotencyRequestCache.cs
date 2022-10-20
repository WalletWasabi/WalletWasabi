using Microsoft.Extensions.Caching.Memory;
using Nito.AsyncEx;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.Cache;

public class IdempotencyRequestCache
{
	public IdempotencyRequestCache(IMemoryCache cache)
	{
		ResponseCache = cache;
	}

	public delegate Task<TResponse> ProcessRequestDelegateAsync<TRequest, TResponse>(TRequest sender, CancellationToken cancellationToken);

	/// <summary>Timeout specifying how long a request response can stay in memory.</summary>
	private static TimeSpan CacheTimeout { get; } = TimeSpan.FromMinutes(5);

	/// <summary>Guards <see cref="ResponseCache"/>.</summary>
	/// <remarks>Unfortunately, <see cref="CacheExtensions.GetOrCreate{TItem}(IMemoryCache, object, Func{ICacheEntry, TItem})"/> is not atomic.</remarks>
	/// <seealso href="https://github.com/dotnet/runtime/issues/36499"/>
	private AsyncLock ResponseCacheLock { get; } = new();

	/// <remarks>Guarded by <see cref="ResponseCacheLock"/>.</remarks>
	private IMemoryCache ResponseCache { get; }

	/// <typeparam name="TRequest">
	/// <see langword="record"/>s are preferred as <see cref="object.GetHashCode"/>
	/// and <see cref="object.Equals(object?)"/> are generated for <see langword="record"/> types automatically.
	/// </typeparam>
	/// <typeparam name="TResponse">Type associated with <typeparamref name="TRequest"/>. The correspondence should be 1:1 mapping.</typeparam>
	public Task<TResponse> GetCachedResponseAsync<TRequest, TResponse>(TRequest request, ProcessRequestDelegateAsync<TRequest, TResponse> action, CancellationToken cancellationToken = default)
		where TRequest : notnull
	{
		MemoryCacheEntryOptions options = new()
		{
			AbsoluteExpiration = DateTimeOffset.UtcNow.Add(CacheTimeout),
		};

		return GetCachedResponseAsync(request, action, options, cancellationToken);
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

		while (true)
		{
			if (!ResponseCache.TryGetValue(request, out TaskCompletionSource<TResponse> responseTcs))
			{
				using (await ResponseCacheLock.LockAsync(cancellationToken).ConfigureAwait(false))
				{
					if (!ResponseCache.TryGetValue(request, out responseTcs))
					{
						callAction = true;
						responseTcs = new();
						ResponseCache.Set(request, responseTcs, options);
					}
				}
			}

			TResponse result;

			if (callAction)
			{
				try
				{
					result = await action(request, cancellationToken).ConfigureAwait(false);
					responseTcs.SetResult(result);
					return result;
				}
				catch (Exception e)
				{
					responseTcs.SetException(e);

					// To avoid unobserved exception.
					await responseTcs.Task.ConfigureAwait(false);
				}
			}
			else
			{
				try
				{
					result = await responseTcs.Task.WithAwaitCancellationAsync(cancellationToken).ConfigureAwait(false);
					return result;
				}
				catch (OperationCanceledException e)
				{
					if (e.CancellationToken == cancellationToken)
					{
						// Cancelled by application shutting down or when the HTTP request is cancelled.
						throw;
					}
				}
				catch (Exception e) when (e is WabiSabiProtocolException or WabiSabiCryptoException)
				{
					throw;
				}
				catch (Exception)
				{
					using (await ResponseCacheLock.LockAsync(cancellationToken).ConfigureAwait(false))
					{
						ResponseCache.Remove(request);
					}
				}
			}
		}
	}

	internal async Task RemoveAsync(string cacheKey)
	{
		using (await ResponseCacheLock.LockAsync().ConfigureAwait(false))
		{
			ResponseCache.Remove(cacheKey);
		}
	}
}
