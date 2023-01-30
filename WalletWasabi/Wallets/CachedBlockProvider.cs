using NBitcoin;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets;

/// <summary>
/// CachedBlockProvider is a blocks provider that keeps the blocks on a repository to satisfy future requests.
/// </summary>
public class CachedBlockProvider : IBlockProvider
{
	public CachedBlockProvider(IRepository<uint256, Block> blockRepository)
	{
		BlockRepository = blockRepository;
	}

	public IRepository<uint256, Block> BlockRepository { get; }

	/// <summary>
	/// Gets a bitcoin block. In case the requested block is not available in the repository it is returned
	/// immediately to the caller; otherwise it obtains the block from the source provider and stores it in
	/// the repository to satisfy future requests.
	/// </summary>
	/// <param name="hash">The block's hash that identifies the requested block.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The requested bitcoin block.</returns>
	public async Task<Block?> TryGetBlockAsync(uint256 hash, CancellationToken cancellationToken)
	{
		return await BlockRepository.TryGetAsync(hash, cancellationToken).ConfigureAwait(false);
	}

	public async Task SaveBlockAsync(Block block, CancellationToken cancellationToken)
	{
		await BlockRepository.SaveAsync(block, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Removes the specified block from the cache repository.
	/// </summary>
	/// <param name="hash">The block's hash that identifies the requested block.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The requested bitcoin block.</returns>
	public Task InvalidateAsync(uint256 hash, CancellationToken cancellationToken)
	{
		return BlockRepository.RemoveAsync(hash, cancellationToken);
	}
}
