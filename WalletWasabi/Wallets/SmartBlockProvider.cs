using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Cache;

namespace WalletWasabi.Wallets;

/// <summary>
/// Block provider that can provide blocks from multiple sources.
/// </summary>
public class SmartBlockProvider
{
	private static MemoryCacheEntryOptions CacheOptions = new()
	{
		Size = 1000,
		SlidingExpiration = TimeSpan.FromSeconds(5)
	};

	public SmartBlockProvider(IRepository<uint256, Block> blockRepository, LocalBlockProvider localBlockProvider, P2pBlockProvider p2PBlockProvider, IMemoryCache cache)
	{
		BlockRepository = blockRepository;
		P2PBlockProvider = p2PBlockProvider;
		LocalBlockProvider = localBlockProvider;
		Cache = new(cache);
	}

	private LocalBlockProvider LocalBlockProvider { get; }
	private P2pBlockProvider P2PBlockProvider { get; }

	private IdempotencyRequestCache Cache { get; }

	public IRepository<uint256, Block> BlockRepository { get; }

	/// <summary>
	/// Gets the block from file-system storage or from other block providers.
	/// </summary>
	/// <exception cref="InvalidOperationException">If the block cannot be obtained.</exception>
	public async Task<Block> GetBlockAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		// Try get the block from the file-system storage.
		Block? result = await BlockRepository.TryGetAsync(blockHash, cancellationToken).ConfigureAwait(false);

		if (result is not null)
		{
			return result;
		}

		// Use the in-memory cache to prevent multiple callers from getting the same block in parallel.
		// The cache makes sure that either in-memory or file-system cache is hit by other callers once we get a block.
		string cacheKey = $"{nameof(GetBlockAsync)}:{blockHash}";

		result = await Cache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken token) => GetBlockNoCacheAsync(blockHash, token),
			options: CacheOptions,
			cancellationToken).ConfigureAwait(false);

		if (result is null)
		{
			throw new InvalidOperationException($"Block {blockHash} could not be downloaded from any source.");
		}

		// Store the block to the file-system.
		await BlockRepository.SaveAsync(result, cancellationToken).ConfigureAwait(false);

		return result;
	}

	/// <summary>
	/// Gets a block without relying on a cache.
	/// </summary>
	/// <remarks>First ask the local block provider (which may or may not be set up) and use the P2P block provider as a fallback.</remarks>
	private async Task<Block?> GetBlockNoCacheAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		return await LocalBlockProvider.TryGetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false)
			?? await P2PBlockProvider.TryGetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Remove the given block from the file-system storage.
	/// </summary>
	/// <remarks>No exception is thrown if there is no such block.</remarks>
	public Task InvalidateAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		return BlockRepository.RemoveAsync(blockHash, cancellationToken);
	}
}
