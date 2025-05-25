using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.Wallets.BlockProviders;

public class FileSystemBlockProvider(FileSystemBlockRepository fs) : IBlockProvider
{
	public Task<Block?> TryGetBlockAsync(uint256 blockHash, CancellationToken cancellationToken) =>
		fs.TryGetBlockAsync(blockHash, cancellationToken);
}
