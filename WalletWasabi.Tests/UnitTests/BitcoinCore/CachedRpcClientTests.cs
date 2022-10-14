using Microsoft.Extensions.Caching.Memory;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

public class CachedRpcClientTests
{
	private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(5);

	/// <summary>
	/// There are two requests for <see cref="CachedRpcClient"/>.
	/// First is being processed and the second must wait until the first is processed and
	/// get the cached value.
	/// </summary>
	[Fact]
	public async Task VerifyThatWaitingRequestDoesNotTriggerActionAsync()
	{
		using CancellationTokenSource testCts = new(TestTimeout);

		using MemoryCache memoryCache = new(new MemoryCacheOptions());
		Mock<CachedRpcClient> mockCachedRpcClient = new(MockBehavior.Strict, new object[] { null!, memoryCache });
		CachedRpcClient cachedRpcClient = mockCachedRpcClient.Object;

		// Action that just increments a variable.
		long blockCount = 0;

		async Task<long> Action(string request, CancellationToken cancellationToken)
		{
			// Introduce artificial delay so that we know that the method is called just once.
			await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

			return Interlocked.Increment(ref blockCount);
		}

		// Simulates GetBlockCount RPC requests.
		string request = "GetBlockCount";

		// Entry options that are not supposed to expire really.
		MemoryCacheEntryOptions cacheEntryOptions = new()
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(100)
		};

		Task<long> task1 = cachedRpcClient.GetDataAsync(request, Action, cacheEntryOptions, testCts.Token);
		Task<long> task2 = cachedRpcClient.GetDataAsync(request, Action, cacheEntryOptions, testCts.Token);

		long[] results = await Task.WhenAll(task1, task2);
		Assert.Equal(new long[] { 1, 1 }, results);
	}
}
