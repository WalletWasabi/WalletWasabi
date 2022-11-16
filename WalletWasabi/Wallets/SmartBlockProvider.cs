using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Cache;

namespace WalletWasabi.Wallets;

/// <summary>
/// <see cref="SmartBlockProvider"/> is a block provider that can provide
/// blocks from multiple requesters.
/// </summary>
public class SmartBlockProvider : IBlockProvider
{
	private static MemoryCacheEntryOptions CacheOptions = new()
	{
		Size = 10,
		SlidingExpiration = TimeSpan.FromSeconds(4)
	};

	public SmartBlockProvider(IBlockProvider provider, IMemoryCache cache)
	{
		InnerBlockProvider = provider;
		Cache = new(cache);
	}

	private IBlockProvider InnerBlockProvider { get; }

	private IdempotencyRequestCache Cache { get; }

	public async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancel)
	{
		string cacheKey = $"{nameof(SmartBlockProvider)}:{nameof(GetBlockAsync)}:{blockHash}";

		return await Cache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken token) => InnerBlockProvider.GetBlockAsync(blockHash, token),
			options: CacheOptions,
			cancel).ConfigureAwait(false);
	}
}
