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
		Size = 10, // Unit size of an item (CacheSize/Size = number of possible items in cache).
		SlidingExpiration = TimeSpan.FromSeconds(5)
	};

	[SuppressMessage("Style", "IDE0004:Remove Unnecessary Cast", Justification = "The cast is necessary to call the other constructor.")]
	public SmartBlockProvider(IFileSystemBlockRepository blockRepository, RpcBlockProvider? rpcBlockProvider, SpecificNodeBlockProvider? specificNodeBlockProvider, P2PBlockProvider? p2PBlockProvider, IMemoryCache cache)
		: this(blockRepository, (IBlockProvider?)rpcBlockProvider, (IBlockProvider?)specificNodeBlockProvider, (IBlockProvider?)p2PBlockProvider, cache)
	{
	}

	internal SmartBlockProvider(IFileSystemBlockRepository blockRepository, IBlockProvider? rpcBlockProvider, IBlockProvider? specificNodeBlockProvider, IBlockProvider? p2PBlockProvider, IMemoryCache cache)
	{
		BlockRepository = blockRepository;
		RpcProvider = rpcBlockProvider;
		SpecificNodeProvider = specificNodeBlockProvider;
		P2PProvider = p2PBlockProvider;
		Cache = new(cache);
	}

	/// <seealso cref="RpcBlockProvider"/>
	private IBlockProvider? RpcProvider { get; }

	/// <seealso cref="SpecificNodeBlockProvider"/>
	private IBlockProvider? SpecificNodeProvider { get; }

	/// <seealso cref="P2PBlockProvider"/>
	private IBlockProvider? P2PProvider { get; }
	private IdempotencyRequestCache Cache { get; }
	private IFileSystemBlockRepository BlockRepository { get; }

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
	/// <remarks>First ask the rpc, then the specific node block provider (both may or may not be set up) and use the P2P block provider as a fallback.</remarks>
	private async Task<Block?> GetBlockNoCacheAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		Block? result = null;

		if (RpcProvider is not null)
		{
			result = await RpcProvider.TryGetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
		}

		if (result is null && SpecificNodeProvider is not null)
		{
			result = await SpecificNodeProvider.TryGetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
		}

		if (result is null && P2PProvider is not null)
		{
			while (true)
			{
				Block? block = await P2PProvider.TryGetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);

				if (block is not null)
				{
					return block;
				}
			}
		}

		return result;
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
