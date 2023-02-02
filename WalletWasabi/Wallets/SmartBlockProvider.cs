using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Cache;

namespace WalletWasabi.Wallets;

/// <summary>
/// Block provider that can provide blocks from multiple sources.
/// </summary>
public class SmartBlockProvider : IBlockProvider
{
	private static MemoryCacheEntryOptions CacheOptions = new()
	{
		Size = 1000,
		SlidingExpiration = TimeSpan.FromSeconds(5)
	};

	[SuppressMessage("Style", "IDE0004:Remove Unnecessary Cast", Justification = "The cast is necessary to call the other constructor.")]
	public SmartBlockProvider(IRepository<uint256, Block> blockRepository, LocalBlockProvider localBlockProvider, P2PBlockProvider p2PBlockProvider, IMemoryCache cache)
		: this(blockRepository, (IBlockProvider)localBlockProvider, (IBlockProvider)p2PBlockProvider, cache)
	{
	}

	/// <summary>
	/// For testing.
	/// </summary>
	internal SmartBlockProvider(IRepository<uint256, Block> blockRepository, IBlockProvider localBlockProvider, IBlockProvider p2PBlockProvider, IMemoryCache cache)
	{
		BlockRepository = blockRepository;
		P2PProvider = p2PBlockProvider;
		LocalProvider = localBlockProvider;
		Cache = new(cache);
	}

	/// <seealso cref="LocalBlockProvider"/>
	private IBlockProvider LocalProvider { get; }

	/// <seealso cref="P2PBlockProvider"/>
	private IBlockProvider P2PProvider { get; }
	private IdempotencyRequestCache Cache { get; }
	private IRepository<uint256, Block> BlockRepository { get; }

	/// <summary>
	/// Tries to get the block from file-system storage or from other block providers.
	/// </summary>
	public async Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		// Try get the block from the file-system storage.
		Block? result = await BlockRepository.TryGetAsync(blockHash, cancellationToken).ConfigureAwait(false);

		if (result is not null)
		{
			return result;
		}

		// Use the in-memory cache to prevent multiple callers from getting the same block in parallel.
		// The cache makes sure that either in-memory or file-system cache is hit by other callers once we get a block.
		string cacheKey = $"{nameof(TryGetBlockAsync)}:{blockHash}";

		result = await Cache.GetCachedResponseAsync(
			cacheKey,
			action: (string request, CancellationToken token) => GetBlockNoCacheAsync(blockHash, token),
			options: CacheOptions,
			cancellationToken).ConfigureAwait(false);

		if (result is not null)
		{
			// Store the block to the file-system.
			await BlockRepository.SaveAsync(result, cancellationToken).ConfigureAwait(false);
		}

		return result;
	}

	/// <summary>
	/// Gets a block without relying on a cache.
	/// </summary>
	/// <remarks>First ask the local block provider (which may or may not be set up) and use the P2P block provider as a fallback.</remarks>
	private async Task<Block?> GetBlockNoCacheAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		return await LocalProvider.TryGetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false)
			?? await P2PProvider.TryGetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Removes the given block from the file-system storage.
	/// </summary>
	/// <remarks>No exception is thrown if there is no such block.</remarks>
	public Task RemoveAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		return BlockRepository.RemoveAsync(blockHash, cancellationToken);
	}
}
