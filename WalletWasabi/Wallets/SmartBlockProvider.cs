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
public class SmartBlockProvider
{
	private static MemoryCacheEntryOptions CacheOptions = new()
	{
		Size = 10,
		SlidingExpiration = TimeSpan.FromSeconds(4)
	};

	public SmartBlockProvider(CachedBlockProvider cachedProvider, LocalBlockProvider localBlockProvider, P2pBlockProvider p2PBlockProvider, IMemoryCache cache)
	{
		CachedProvider = cachedProvider;
		P2PBlockProvider = p2PBlockProvider;
		LocalBlockProvider = localBlockProvider;
		Cache = new(cache);
	}

	private CachedBlockProvider CachedProvider { get; }
	private LocalBlockProvider LocalBlockProvider { get; }
	private P2pBlockProvider P2PBlockProvider { get; }

	private IdempotencyRequestCache Cache { get; }
	
	public IRepository<uint256, Block> BlockRepository => CachedProvider.BlockRepository;

	public async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancel)
	{
		Block? result = await CachedProvider.TryGetBlockAsync(blockHash, cancel).ConfigureAwait(false);
		
		if (result is not null)
		{
			return result;
		}
		
		string cacheKey = $"{nameof(SmartBlockProvider)}:{nameof(GetBlockAsync)}:{blockHash}";

		result = await Cache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken token) => GetBlockNoCacheAsync(blockHash, token),
			options: CacheOptions,
			cancel).ConfigureAwait(false);

		if (result is null)
		{
			throw new InvalidOperationException($"Block {blockHash} could not be downloaded from any source.");
		}

		await CachedProvider.SaveBlockAsync(result, cancel).ConfigureAwait(false);
		
		return result;
	}

	private async Task<Block?> GetBlockNoCacheAsync(uint256 blockHash, CancellationToken cancel)
	{
		return await LocalBlockProvider.TryGetBlockAsync(blockHash, cancel).ConfigureAwait(false) ??
		       await P2PBlockProvider.TryGetBlockAsync(blockHash, cancel).ConfigureAwait(false);
	}
	
	public Task InvalidateAsync(uint256 hash, CancellationToken cancellationToken)
	{
		return CachedProvider.InvalidateAsync(hash, cancellationToken);
	}
	
}
