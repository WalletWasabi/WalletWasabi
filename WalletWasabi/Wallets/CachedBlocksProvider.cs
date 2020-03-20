using NBitcoin;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets
{
	/// <summary>
	/// CachedBlocksProvider is a blocks provider that keep the blocks on a repository to satify future requests.
	/// </summary>
	public class CachedBlocksProvider : IBlocksProvider
	{
		public CachedBlocksProvider(IBlocksProvider blocksSourceProvider, IRepository<uint256, Block> blocksRepository)
		{
			BlocksRepository = blocksRepository;
			BlocksSourceProvider = blocksSourceProvider;
		}

		public IRepository<uint256, Block> BlocksRepository { get; } 
		public IBlocksProvider BlocksSourceProvider { get; }

		/// <summary>
		/// Gets a bitcoin block. In case the requested block is not available in the repository it is returned 
		/// inmediately to the caller; otherwise it obtains the block from the source provider and store it in
		/// the repository to satisfy future requests.
		/// </summary>
		/// <param name="hash">The block's hash that identifies the requested block.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The requested bitcoin block.</returns>
		public async Task<Block> GetBlockAsync(uint256 hash, CancellationToken cancellationToken)
		{
			Block block = await BlocksRepository.GetAsync(hash, cancellationToken);
			if (block is null)
			{
				block = await BlocksSourceProvider.GetBlockAsync(hash, cancellationToken);
				await BlocksRepository.SaveAsync(block, cancellationToken);
			}
			return block;
		}

		/// <summary>
		/// Removes the specified block from the cache repository.
		/// </summary>
		/// <param name="hash">The block's hash that identifies the requested block.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The requested bitcoin block.</returns>
		public Task InvalidateAsync(uint256 hash, CancellationToken cancellationToken)
		{
			return BlocksRepository.RemoveAsync(hash, cancellationToken);
		}
	}
}