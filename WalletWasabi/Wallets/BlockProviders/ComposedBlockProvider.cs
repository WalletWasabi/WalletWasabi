using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.Wallets.BlockProviders;

public class ComposedBlockProvider(IBlockProvider[] blockProviders) : IBlockProvider
{
	public async Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		foreach (var blockProvider in blockProviders)
		{
			var block = await blockProvider.TryGetBlockAsync(blockHash, cancellationToken).ConfigureAwait(false);

			if (block is not null)
			{
				return block;
			}
		}

		return null;
	}
}
