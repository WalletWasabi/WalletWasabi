using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.Wallets.BlockProviders;

public class CachedBlockProvider(IBlockProvider blockProvider, IFileSystemBlockRepository fs) : IBlockProvider
{
	public async Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		var block = await blockProvider.TryGetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);
		if (block is not null)
		{
			await fs.SaveAsync(block, cancellationToken).ConfigureAwait(false);
		}

		return block;
	}
}
