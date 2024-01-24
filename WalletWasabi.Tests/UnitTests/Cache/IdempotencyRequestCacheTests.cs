using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Cache;
using WalletWasabi.WabiSabi.Backend.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Cache;

/// <summary>
/// Tests for <see cref="IdempotencyRequestCache"/> class.
/// </summary>
public class IdempotencyRequestCacheTests
{
	/// <summary>
	/// Very basic test that a correct response is returned.
	/// </summary>
	[Fact]
	public async Task BasicCacheBehaviorAsync()
	{
		using MemoryCache memoryCache = new(new MemoryCacheOptions());
		IdempotencyRequestCache cache = new(memoryCache);

		SimpleRequestType request = new(); // A request that is presented for the first time.
		SimpleResponseType preparedResponse = new(); // Pre-prepared response for testing purposes.

		SimpleResponseType response = await cache.GetCachedResponseAsync(request, action: (request, cancellationToken) => Task.FromResult(preparedResponse));

		// Verify that the response is really what we pre-prepared
		Assert.NotNull(response);
		Assert.IsType<SimpleResponseType>(response);
		Assert.Same(preparedResponse, response); // Compare by reference.
	}

	/// <summary>
	/// Simulates two simultaneous requests and both have the same chance of succeeding.
	/// One of the requests should be served from cache.
	/// </summary>
	[Fact]
	public async Task TwoSimultaneousCacheRequestsAsync()
	{
		using MemoryCache memoryCache = new(new MemoryCacheOptions());
		IdempotencyRequestCache cache = new(memoryCache);

		SimpleRequestType request = new(); // A request that is presented for the first time.
		SimpleResponseType preparedResponse = new(); // Pre-prepared response for testing purposes.

		// How many times the GetCachedResponseAsync action was called.
		long counter = 0;

		Task<SimpleResponseType> task1 = cache.GetCachedResponseAsync(request, action: GetResponseAsync);
		Task<SimpleResponseType> task2 = cache.GetCachedResponseAsync(request, action: GetResponseAsync);

		SimpleResponseType[] responses = await Task.WhenAll(task1, task2);

		// Verify that GetCachedResponseAsync action delegate is called only once.
		Assert.Equal(1, Interlocked.Read(ref counter));

		// Two responses must come.
		Assert.Equal(2, responses.Length);

		// ... and they must represent the same object (compared by reference).
		Assert.Same(responses[0], responses[1]);

		Task<SimpleResponseType> GetResponseAsync(SimpleRequestType request, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref counter);
			return Task.FromResult(preparedResponse);
		}
	}

	/// <summary>
	/// If the first request fails with <see cref="WabiSabiProtocolException"/>,
	/// then the second request will try again.
	/// </summary>
	[Fact]
	public async Task FailureIsNotCachedAsync()
	{
		using MemoryCache memoryCache = new(new MemoryCacheOptions());
		IdempotencyRequestCache cache = new(memoryCache);

		SimpleRequestType request = new(); // A request that is presented for the first time.
		SimpleResponseType preparedResponse = new(); // Pre-prepared response for testing purposes.

		// First request.
		InvalidOperationException ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetCachedResponseAsync(request, action: ExceptionAsync));

		// Second request.
		SimpleResponseType response = await cache.GetCachedResponseAsync(request, action: SuccessAsync);
		Assert.Same(preparedResponse, response);

		Task<SimpleResponseType> ExceptionAsync(SimpleRequestType request, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException();
		}

		Task<SimpleResponseType> SuccessAsync(SimpleRequestType request, CancellationToken cancellationToken)
		{
			return Task.FromResult(preparedResponse);
		}
	}

	/// <summary>
	/// Cache key comparisons are done by GethHashCode and Equals implementation of "request" types.
	/// <see cref="HashCodeRequestType"/> has its own <see cref="object.GetHashCode"/> and <see cref="object.Equals(object?)"/> implementation.
	/// </summary>
	[Fact]
	public async Task RequestsAreCachedByHashCode1Async()
	{
		using MemoryCache memoryCache = new(new MemoryCacheOptions());
		IdempotencyRequestCache cache = new(memoryCache);

		HashCodeRequestType request1 = new(hashCode: 7, importantValue: 42);
		HashCodeRequestType request2 = new(hashCode: 7, importantValue: 24);
		HashCodeRequestType request3 = new(hashCode: 14, importantValue: 24);

		HashCodeResponseType response1 = await cache.GetCachedResponseAsync(request1, action: ReturnNewAsync);
		HashCodeResponseType response2 = await cache.GetCachedResponseAsync(request2, action: ReturnNewAsync);
		HashCodeResponseType response3 = await cache.GetCachedResponseAsync(request3, action: ReturnNewAsync);

		// The same hash code of two objects lead to the same response even some object values are different.
		Assert.Same(response1, response2);

		// request3 has different hash code (14) than other requests (7).
		Assert.NotSame(response1, response3);
		Assert.NotSame(response2, response3);

		static Task<HashCodeResponseType> ReturnNewAsync(HashCodeRequestType request, CancellationToken cancellationToken)
		{
			return Task.FromResult(new HashCodeResponseType());
		}
	}

	/// <summary>
	/// Cache key comparisons are done by GethHashCode and Equals implementation of "request" types.
	/// </summary>
	[Fact]
	public async Task RequestsAreCachedByHashCode2Async()
	{
		using MemoryCache memoryCache = new(new MemoryCacheOptions());
		IdempotencyRequestCache cache = new(memoryCache);

		ComplexRequestType request1 = new(ImportantValue: 7, ParentHash: uint256.One);
		ComplexRequestType request2 = new(ImportantValue: 7, ParentHash: uint256.One);
		ComplexRequestType request3 = new(ImportantValue: 7, ParentHash: uint256.Zero);

		ComplexResponseType response1 = await cache.GetCachedResponseAsync(request1, action: ReturnNewAsync);
		ComplexResponseType response2 = await cache.GetCachedResponseAsync(request2, action: ReturnNewAsync);
		ComplexResponseType response3 = await cache.GetCachedResponseAsync(request3, action: ReturnNewAsync);

		// The same hash code of two objects lead to the same response even some object values are different.
		Assert.Same(response1, response2);

		// request3 has different hash code (14) than other requests (7).
		Assert.NotSame(response1, response3);
		Assert.NotSame(response2, response3);

		static Task<ComplexResponseType> ReturnNewAsync(ComplexRequestType request, CancellationToken cancellationToken)
		{
			return Task.FromResult(new ComplexResponseType());
		}
	}

	/// <summary>
	/// Simulates: First cache request is being processed. Second is waiting for the first one to finish.
	/// But then the first cache request is cancelled (suppose that the first request is a long running RPC request).
	/// </summary>
	[Fact]
	public async Task CancelledFirstRequestAsync()
	{
		// To cancel cache request processing.
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(1));

		using MemoryCache memoryCache = new(new MemoryCacheOptions());
		IdempotencyRequestCache cache = new(memoryCache);

		// To know that the first request is being computed already.
		TaskCompletionSource tcsInFactory = new();

		// To throw an exception when the first request is being computed.
		TaskCompletionSource tcsFactoryResult = new();

		Task<int> request1Task = cache.GetCachedResponseAsync("some-operation", action: ReturnNewAsync, testDeadlineCts.Token);
		Task<int> request2Task = cache.GetCachedResponseAsync("some-operation", action: ReturnNewAsync, testDeadlineCts.Token);

		// Wait until the first request is being computed and the second is waiting for the first one to finish.
		await tcsInFactory.Task;

		// The first request ends up throwing an exception. Should stop processing of the second request.
		tcsFactoryResult.SetException(new ArgumentOutOfRangeException());

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => request1Task);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => request2Task);

		async Task<int> ReturnNewAsync(string request, CancellationToken cancellationToken)
		{
			tcsInFactory.TrySetResult();
			await tcsFactoryResult.Task.ConfigureAwait(false);
			throw new NotSupportedException("Function never returns anything.");
		}
	}

	private record SimpleRequestType();
	private record SimpleResponseType();

	private record ComplexRequestType(int ImportantValue, uint256 ParentHash);
	private record ComplexResponseType();

	/// <summary>Type that allows us to specify arbitrary object hash code.</summary>
	private class HashCodeRequestType : IEquatable<HashCodeRequestType>
	{
		public HashCodeRequestType(int hashCode, int importantValue)
		{
			HashCode = hashCode;
			ImportantValue = importantValue;
		}

		public int HashCode { get; }
		public int ImportantValue { get; }

		public override bool Equals(object? obj) => Equals(obj as HashCodeRequestType);

		public bool Equals(HashCodeRequestType? other)
		{
			return other is not null && HashCode == other.HashCode;
		}

		public override int GetHashCode()
		{
			return HashCode;
		}
	}

	private class HashCodeResponseType
	{
	}
}
