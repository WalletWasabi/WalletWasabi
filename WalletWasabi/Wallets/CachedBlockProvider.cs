using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;

namespace WalletWasabi.Wallets;

/// <summary>
/// CachedBlockProvider is a blocks provider that keeps the blocks on a repository to satisfy future requests.
/// </summary>
public class CachedBlockProvider : IBlockProvider
{
	public CachedBlockProvider(IBlockProvider blockSourceProvider, BlockRepository blockRepository)
	{
		BlockRepository = blockRepository;
		BlockSourceProvider = blockSourceProvider;
	}

	public BlockRepository BlockRepository { get; }
	public IBlockProvider BlockSourceProvider { get; }

	/// <summary>
	/// Gets a bitcoin block. In case the requested block is not available in the repository it is returned
	/// immediately to the caller; otherwise it obtains the block from the source provider and stores it in
	/// the repository to satisfy future requests.
	/// </summary>
	/// <param name="hash">The block's hash that identifies the requested block.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The requested bitcoin block.</returns>
	public async Task<Block> GetBlockAsync(uint256 hash, CancellationToken cancellationToken)
	{
		Block? block = await BlockRepository.TryGetAsync(hash, cancellationToken).ConfigureAwait(false);
		if (block is null)
		{
			block = await BlockSourceProvider.GetBlockAsync(hash, cancellationToken).ConfigureAwait(false);
			BlockRepository.Add(block);
		}
		return block;
	}

	/// <summary>
	/// Removes the specified block from the cache repository.
	/// </summary>
	/// <param name="hash">The block's hash that identifies the requested block.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The requested bitcoin block.</returns>
	public Task InvalidateAsync(uint256 hash, CancellationToken cancellationToken) => BlockRepository.RemoveAsync(hash, cancellationToken);
}
